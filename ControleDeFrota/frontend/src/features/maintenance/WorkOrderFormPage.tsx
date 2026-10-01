import { useState } from 'react';
import { ActionIcon, Box, Button, Checkbox, Group, Paper, Select, Stack, TextInput, Textarea } from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { useNavigate, useParams } from 'react-router-dom';
import { toApiError } from '../../api/errors';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { VehiclePicker } from '../../components/EntityPickers';
import { FormActions, FormSection } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { formatPlate } from '../../lib/format';
import { workOrdersApi } from './api';
import {
  MAINTENANCE_PRIORITY, MAINTENANCE_TYPE, workshopsApi,
  type MaintenancePriority, type MaintenanceType, type WorkOrder, type WorkOrderItemRequest,
} from './maintenance';

type ItemState = WorkOrderItemRequest & { key: string };

const newItem = (): ItemState => ({ key: crypto.randomUUID(), id: null, description: '', maintenancePlanItemId: null, isRequired: true, notes: null });

export function WorkOrderFormPage() {
  const { id } = useParams();
  const detail = workOrdersApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(w) => <WorkOrderForm workOrder={w} />}</EntityFormPage>;
}

function WorkOrderForm({ workOrder: w }: { workOrder?: WorkOrder }) {
  const navigate = useNavigate();
  const save = workOrdersApi.useSave(w?.id);
  const workshops = workshopsApi.useList({ status: 'Active', pageSize: 100 });

  const [vehicleId, setVehicleId] = useState<string | null>(w?.vehicleId ?? null);
  const [workshopId, setWorkshopId] = useState<string | null>(w?.workshopId ?? null);
  const [type, setType] = useState<MaintenanceType>(w?.type ?? 'Corrective');
  const [priority, setPriority] = useState<MaintenancePriority>(w?.priority ?? 'Medium');
  const [scheduledAt, setScheduledAt] = useState<Date | null>(w?.scheduledAt ? new Date(w.scheduledAt) : null);
  const [description, setDescription] = useState(w?.description ?? '');
  const [diagnosis, setDiagnosis] = useState(w?.diagnosis ?? '');
  const [notes, setNotes] = useState(w?.notes ?? '');
  const [items, setItems] = useState<ItemState[]>(() => w?.items.map((i) => ({ ...i, key: i.id })) ?? []);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const update = (key: string, change: Partial<ItemState>) => setItems((all) => all.map((i) => (i.key === key ? { ...i, ...change } : i)));

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    save.mutate({
      vehicleId, implementId: null, workshopId, type, priority, scheduledAt: scheduledAt?.toISOString() ?? null,
      description, diagnosis: diagnosis || null, notes: notes || null,
      items: items.map((i) => ({ ...i, notes: i.notes || null })),
    }, {
      onSuccess: (saved) => { notifySuccess(w ? 'Ordem de serviço atualizada.' : `Ordem de serviço ${saved.number} aberta.`); navigate(`/ordens-servico/${saved.id}`); },
      onError: (err) => { setErrors(toApiError(err).fieldErrors); notifyError(err, 'A ordem de serviço não foi salva'); },
    });
  };

  return (
    <>
      <PageHeader title={w ? w.number : 'Nova ordem de serviço'}
        description="Campos com * são obrigatórios."
        breadcrumbs={[{ label: 'Ordens de serviço', to: '/ordens-servico' }, { label: w ? w.number : 'Nova' }]} />
      <form onSubmit={submit} noValidate>
        <Stack gap="md">
          <FormSection title="Identificação" cols={3}>
            <VehiclePicker label="Veículo" value={vehicleId} onChange={setVehicleId} disabled={!!w}
              initialLabel={w ? formatPlate(w.licensePlate) : null} error={errors.vehicleId} />
            <Select label="Oficina" placeholder="Interna / a definir" clearable allowDeselect data={(workshops.data?.items ?? []).map((ws) => ({ value: ws.id, label: ws.name }))}
              value={workshopId} onChange={setWorkshopId} searchable />
            <DateTimePicker label="Agendada para" valueFormat="DD/MM/YYYY HH:mm" value={scheduledAt} onChange={setScheduledAt} clearable />
            <Select label="Tipo" withAsterisk data={toSelectData(MAINTENANCE_TYPE)} value={type} onChange={(v) => setType(v as MaintenanceType)} />
            <Select label="Prioridade" withAsterisk data={toSelectData(MAINTENANCE_PRIORITY)} value={priority} onChange={(v) => setPriority(v as MaintenancePriority)} />
          </FormSection>

          <FormSection title="O problema" cols={1}>
            <Textarea label="Descrição" withAsterisk autosize minRows={3} maxLength={2000} value={description}
              onChange={(e) => setDescription(e.currentTarget.value)} error={errors.description} />
            <Textarea label="Diagnóstico (opcional)" autosize minRows={2} maxLength={2000} value={diagnosis} onChange={(e) => setDiagnosis(e.currentTarget.value)} />
          </FormSection>

          <FormSection title={`Itens a executar (${items.length})`} description="Opcional. Itens obrigatórios precisam ser concluídos ou dispensados antes de fechar a ordem." cols={1}>
            <Stack gap="sm">
              {items.map((item) => (
                <Paper key={item.key} p="sm" withBorder>
                  <Group gap="xs" wrap="nowrap" align="flex-start">
                    <TextInput placeholder="Ex.: trocar pastilhas de freio" maxLength={300} value={item.description} style={{ flex: 1 }}
                      onChange={(e) => update(item.key, { description: e.currentTarget.value })} />
                    <Checkbox label="Obrigatório" checked={item.isRequired} mt={6} onChange={(e) => update(item.key, { isRequired: e.currentTarget.checked })} />
                    <ActionIcon variant="subtle" color="red" onClick={() => setItems((all) => all.filter((i) => i.key !== item.key))} aria-label="Remover item"><IconTrash size={16} /></ActionIcon>
                  </Group>
                </Paper>
              ))}
              <Button variant="default" leftSection={<IconPlus size={16} />} onClick={() => setItems((all) => [...all, newItem()])}>Adicionar item</Button>
            </Stack>
          </FormSection>

          <FormSection title="Observações" cols={1}>
            <Textarea aria-label="Observações" autosize minRows={2} maxLength={2000} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />
          </FormSection>
        </Stack>
        <Box mt="md">
          <FormActions saving={save.isPending} onCancel={() => navigate(w ? `/ordens-servico/${w.id}` : '/ordens-servico')} submitLabel={w ? 'Salvar alterações' : 'Abrir ordem de serviço'} />
        </Box>
      </form>
    </>
  );
}
