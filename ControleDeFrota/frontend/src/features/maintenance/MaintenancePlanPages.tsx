import { useState } from 'react';
import {
  ActionIcon, Badge, Box, Button, Checkbox, Group, NumberInput, Paper, Select, SimpleGrid, Skeleton, Stack, Switch, Table, Text, TextInput,
} from '@mantine/core';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { useNavigate, useParams } from 'react-router-dom';
import { toApiError } from '../../api/errors';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { FormActions, FormSection, confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { VehiclePicker } from '../../components/EntityPickers';
import { formatPlate } from '../../lib/format';
import { VEHICLE_TYPE, type VehicleType } from '../vehicles/vehicles';
import { maintenancePlansApi } from './api';
import { MAINTENANCE_PRIORITY, type MaintenancePlan, type MaintenancePlanItemRequest, type MaintenancePriority } from './maintenance';

export function MaintenancePlanListPage() {
  const navigate = useNavigate();
  const query = maintenancePlansApi.useList(true);
  const remove = maintenancePlansApi.useRemove();
  const newButton = <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/planos-manutencao/novo')}>Novo plano</Button>;

  return (
    <>
      <PageHeader title="Planos de manutenção" description="Intervalos de manutenção preventiva por km, meses ou horas. O plano do veículo vale antes do plano do tipo, que vale antes do padrão da empresa."
        action={newButton} />
      {query.isLoading && <Skeleton height={200} />}
      {query.error && <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper>}
      {query.data?.length === 0 && (
        <Paper><EmptyState icon={<IconPlus size={28} />} title="Nenhum plano cadastrado" description="Crie o plano padrão da empresa, ou um plano por tipo ou veículo específico." action={newButton} /></Paper>
      )}
      {query.data && query.data.length > 0 && (
        <Paper style={{ overflow: 'hidden' }}>
          <Table.ScrollContainer minWidth={560}>
            <Table highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr><Table.Th>Plano</Table.Th><Table.Th>Aplica-se a</Table.Th><Table.Th>Itens</Table.Th><Table.Th w={56} aria-label="Ações" /></Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {query.data.map((p) => (
                  <Table.Tr key={p.id} style={{ cursor: 'pointer', opacity: p.isActive ? 1 : 0.6 }} onClick={() => navigate(`/planos-manutencao/${p.id}`)}>
                    <Table.Td>
                      <Text size="sm" fw={600}>{p.name}</Text>
                      {!p.isActive && <Badge size="xs" color="gray">Inativo</Badge>}
                    </Table.Td>
                    <Table.Td>
                      {p.vehicleId ? `Veículo ${formatPlate(p.vehicleLicensePlate!)}` : p.vehicleType ? VEHICLE_TYPE[p.vehicleType].label : 'Padrão da empresa'}
                    </Table.Td>
                    <Table.Td>{p.itemCount}</Table.Td>
                    <Table.Td onClick={(e) => e.stopPropagation()}>
                      <ActionIcon variant="subtle" color="red" aria-label={`Excluir ${p.name}`} onClick={() => confirmDelete({
                        entity: 'plano', name: p.name,
                        onConfirm: () => remove.mutate(p.id, { onSuccess: () => notifySuccess('Plano excluído.'), onError: (e) => notifyError(e, 'O plano não foi excluído') }),
                      })}><IconTrash size={16} /></ActionIcon>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Paper>
      )}
    </>
  );
}

export function MaintenancePlanFormPage() {
  const { id } = useParams();
  const detail = maintenancePlansApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(p) => <PlanForm plan={p} />}</EntityFormPage>;
}

type ScopeKind = 'default' | 'type' | 'vehicle';
type ItemState = MaintenancePlanItemRequest & { key: string };

const newItem = (): ItemState => ({
  key: crypto.randomUUID(), id: null, serviceName: '', intervalKm: null, intervalMonths: null, intervalHours: null,
  graceKm: null, graceDays: null, graceHours: null, priority: 'Medium', estimatedDurationMinutes: null, estimatedCost: null,
  isRequired: true, notes: null,
});

function PlanForm({ plan }: { plan?: MaintenancePlan }) {
  const navigate = useNavigate();
  const save = maintenancePlansApi.useSave(plan?.id);
  const [name, setName] = useState(plan?.name ?? '');
  const [scope, setScope] = useState<ScopeKind>(plan?.vehicleId ? 'vehicle' : plan?.vehicleType ? 'type' : 'default');
  const [vehicleId, setVehicleId] = useState<string | null>(plan?.vehicleId ?? null);
  const [vehicleType, setVehicleType] = useState<VehicleType | null>(plan?.vehicleType ?? null);
  const [isActive, setIsActive] = useState(plan?.isActive ?? true);
  const [items, setItems] = useState<ItemState[]>(() => plan?.items.map((i) => ({ ...i, key: i.id })) ?? [newItem()]);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const update = (key: string, change: Partial<ItemState>) => setItems((all) => all.map((i) => (i.key === key ? { ...i, ...change } : i)));

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    save.mutate({
      name,
      vehicleId: scope === 'vehicle' ? vehicleId : null,
      vehicleType: scope === 'type' ? vehicleType : null,
      isActive,
      items: items.map((i) => ({ ...i, estimatedCost: i.estimatedCost ?? null })),
    }, {
      onSuccess: () => { notifySuccess('Plano salvo com sucesso.'); navigate('/planos-manutencao'); },
      onError: (err) => { setErrors(toApiError(err).fieldErrors); notifyError(err, 'O plano não foi salvo'); },
    });
  };

  return (
    <>
      <PageHeader title={plan ? plan.name : 'Novo plano de manutenção'}
        description="Campos com * são obrigatórios. Cada item precisa de pelo menos um intervalo (km, meses ou horas)."
        breadcrumbs={[{ label: 'Planos de manutenção', to: '/planos-manutencao' }, { label: plan ? 'Editar' : 'Novo' }]} />
      <form onSubmit={submit} noValidate>
        <Stack gap="md">
          <FormSection title="Identificação" cols={2}>
            <TextInput label="Nome" withAsterisk maxLength={150} value={name} onChange={(e) => setName(e.currentTarget.value)} error={errors.name} />
            <Switch label="Ativo (disponível para novos veículos)" checked={isActive} onChange={(e) => setIsActive(e.currentTarget.checked)} mt="lg" />
            <Select label="Aplica-se a" withAsterisk data={[
              { value: 'default', label: 'Padrão da empresa' },
              { value: 'type', label: 'Um tipo de veículo' },
              { value: 'vehicle', label: 'Um veículo específico' },
            ]} value={scope} onChange={(v) => setScope(v as ScopeKind)} />
            {scope === 'type' && (
              <Select label="Tipo de veículo" withAsterisk data={toSelectData(VEHICLE_TYPE)} value={vehicleType} onChange={(v) => setVehicleType(v as VehicleType)} />
            )}
            {scope === 'vehicle' && (
              <VehiclePicker label="Veículo" value={vehicleId} onChange={setVehicleId}
                initialLabel={plan?.vehicleLicensePlate ? formatPlate(plan.vehicleLicensePlate) : null} error={errors.vehicleId} />
            )}
          </FormSection>

          <FormSection title={`Itens (${items.length})`} description="O primeiro intervalo (km, meses ou horas) a vencer dispara a manutenção. A carência vale como tolerância depois e aviso antes do vencimento." cols={1}>
            <Stack gap="sm">
              {errors.items && <Text c="red" size="sm">{errors.items}</Text>}
              {items.map((item) => (
                <Paper key={item.key} p="sm" withBorder>
                  <Stack gap="xs">
                    <Group gap="xs" wrap="nowrap" align="flex-start">
                      <TextInput label="Serviço" withAsterisk maxLength={150} value={item.serviceName} style={{ flex: 1 }}
                        onChange={(e) => update(item.key, { serviceName: e.currentTarget.value })} />
                      <Select label="Prioridade" data={toSelectData(MAINTENANCE_PRIORITY)} value={item.priority} w={140}
                        onChange={(v) => update(item.key, { priority: v as MaintenancePriority })} />
                      <Checkbox label="Obrigatório" checked={item.isRequired} mt={28} onChange={(e) => update(item.key, { isRequired: e.currentTarget.checked })} />
                      <ActionIcon variant="subtle" color="red" mt={22} disabled={items.length === 1}
                        onClick={() => setItems((all) => all.filter((i) => i.key !== item.key))} aria-label="Remover item"><IconTrash size={16} /></ActionIcon>
                    </Group>
                    <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }}>
                      <NumberInput label="A cada" size="xs" suffix=" km" allowNegative={false} allowDecimal={false} thousandSeparator="."
                        value={item.intervalKm ?? ''} onChange={(v) => update(item.key, { intervalKm: v === '' ? null : Number(v) })} />
                      <NumberInput label="Carência" size="xs" suffix=" km" allowNegative={false} allowDecimal={false} thousandSeparator="."
                        value={item.graceKm ?? ''} onChange={(v) => update(item.key, { graceKm: v === '' ? null : Number(v) })} />
                      <NumberInput label="A cada" size="xs" suffix=" meses" allowNegative={false} allowDecimal={false}
                        value={item.intervalMonths ?? ''} onChange={(v) => update(item.key, { intervalMonths: v === '' ? null : Number(v) })} />
                      <NumberInput label="Carência" size="xs" suffix=" dias" allowNegative={false} allowDecimal={false}
                        value={item.graceDays ?? ''} onChange={(v) => update(item.key, { graceDays: v === '' ? null : Number(v) })} />
                      <NumberInput label="A cada" size="xs" suffix=" h" allowNegative={false} decimalScale={1}
                        value={item.intervalHours ?? ''} onChange={(v) => update(item.key, { intervalHours: v === '' ? null : Number(v) })} />
                      <NumberInput label="Carência" size="xs" suffix=" h" allowNegative={false} decimalScale={1}
                        value={item.graceHours ?? ''} onChange={(v) => update(item.key, { graceHours: v === '' ? null : Number(v) })} />
                    </SimpleGrid>
                    <SimpleGrid cols={{ base: 1, sm: 3 }}>
                      <NumberInput label="Duração estimada" size="xs" suffix=" min" allowNegative={false} allowDecimal={false}
                        value={item.estimatedDurationMinutes ?? ''} onChange={(v) => update(item.key, { estimatedDurationMinutes: v === '' ? null : Number(v) })} />
                      <NumberInput label="Custo estimado" size="xs" prefix="R$ " decimalScale={2} allowNegative={false} thousandSeparator="." decimalSeparator=","
                        value={item.estimatedCost ?? ''} onChange={(v) => update(item.key, { estimatedCost: v === '' ? null : Number(v) })} />
                      <TextInput label="Observações" size="xs" maxLength={1000} value={item.notes ?? ''} onChange={(e) => update(item.key, { notes: e.currentTarget.value || null })} />
                    </SimpleGrid>
                  </Stack>
                </Paper>
              ))}
              <Button variant="default" leftSection={<IconPlus size={16} />} onClick={() => setItems((all) => [...all, newItem()])}>
                Adicionar item
              </Button>
            </Stack>
          </FormSection>
        </Stack>
        <Box mt="md">
          <FormActions saving={save.isPending} onCancel={() => navigate('/planos-manutencao')} submitLabel={plan ? 'Salvar plano' : 'Criar plano'} />
        </Box>
      </form>
    </>
  );
}
