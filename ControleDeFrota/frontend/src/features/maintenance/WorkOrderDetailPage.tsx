import { useState } from 'react';
import {
  ActionIcon, Anchor, Button, Group, Modal, NumberInput, Paper, SimpleGrid, Stack, Table, Text, TextInput, Textarea,
} from '@mantine/core';
import { IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { HeaderFact, InfoGrid, Section } from '../../components/DetailLayout';
import { EntityFormPage } from '../../components/EntityFormPage';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { formatCurrency, formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { workOrdersApi } from './api';
import {
  MAINTENANCE_PRIORITY, MAINTENANCE_TYPE, WORK_ORDER_ACTION, WORK_ORDER_ITEM_STATUS, WORK_ORDER_STATUS,
  type WorkOrder, type WorkOrderStatus,
} from './maintenance';

export function WorkOrderDetailPage() {
  const { id } = useParams();
  const detail = workOrdersApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(w) => w && <WorkOrderDetail workOrder={w} />}</EntityFormPage>;
}

const EDITABLE_STATUSES: WorkOrderStatus[] = ['Draft', 'Approved', 'Scheduled'];

function WorkOrderDetail({ workOrder: w }: { workOrder: WorkOrder }) {
  const { can } = useAuth();
  const canManage = can(PERMISSIONS.maintenance.manageworkorders);
  const canViewCosts = can(PERMISSIONS.maintenance.viewcosts);
  const [target, setTarget] = useState<WorkOrderStatus | null>(null);

  const actions = canManage && w.nextStatuses.length > 0 && (
    <Group gap="xs">
      {w.nextStatuses.map((s) => (
        <Button key={s} variant={s === 'Completed' ? 'filled' : 'default'} color={s === 'Cancelled' || s === 'Rejected' ? 'red' : undefined} onClick={() => setTarget(s)}>
          {WORK_ORDER_ACTION[s]}
        </Button>
      ))}
    </Group>
  );

  return (
    <>
      <PageHeader
        title={w.number}
        description={`Aberta em ${formatDateTime(w.openedAt)} por ${w.createdByName ?? 'Sistema'}.`}
        breadcrumbs={[{ label: 'Ordens de serviço', to: '/ordens-servico' }, { label: w.number }]}
        action={
          <Group gap="xs">
            <AuditHistoryButton entity="WorkOrder" id={w.id} />
            {canManage && EDITABLE_STATUSES.includes(w.status) && (
              <Button component={Link} to={`/ordens-servico/${w.id}/editar`} variant="default" leftSection={<IconPencil size={18} />}>Editar</Button>
            )}
          </Group>
        }
      />
      <Stack gap="md">
        <Paper p="md">
          <Group justify="space-between" gap="sm" wrap="wrap">
            <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="md" style={{ flex: 1 }}>
              <HeaderFact label="Situação" value={<StatusBadge value={w.status} map={WORK_ORDER_STATUS} />} />
              <HeaderFact label="Veículo" value={<Anchor component={Link} to={`/veiculos/${w.vehicleId}?aba=manutencao`} ff="monospace" fw={700} size="sm">{formatPlate(w.licensePlate)}</Anchor>} />
              <HeaderFact label="Tipo" value={MAINTENANCE_TYPE[w.type].label} />
              <HeaderFact label="Prioridade" value={<StatusBadge value={w.priority} map={MAINTENANCE_PRIORITY} />} />
            </SimpleGrid>
            {actions}
          </Group>
        </Paper>

        <Section title="O problema">
          <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>{w.description}</Text>
          <InfoGrid items={[
            { label: 'Oficina', value: w.workshopName },
            { label: 'Agendada para', value: w.scheduledAt && formatDateTime(w.scheduledAt) },
            { label: 'Iniciada em', value: w.startedAt && formatDateTime(w.startedAt) },
            { label: 'Concluída em', value: w.completedAt && formatDateTime(w.completedAt) },
            { label: 'Diagnóstico', value: w.diagnosis },
            { label: 'Resolução', value: w.resolution },
          ]} />
        </Section>

        {w.items.length > 0 && <ItemsSection workOrder={w} canManage={canManage} />}
        <PartsSection workOrder={w} canManage={canManage} canViewCosts={canViewCosts} />
        <LaborSection workOrder={w} canManage={canManage} canViewCosts={canViewCosts} />

        {canViewCosts && (
          <Section title="Custo">
            <InfoGrid items={[
              { label: 'Peças', value: formatCurrency(w.partsCost) },
              { label: 'Mão de obra', value: formatCurrency(w.laborCost) },
              { label: 'Outros', value: formatCurrency(w.otherCost) },
              { label: 'Total', value: <Text fw={700}>{formatCurrency(w.totalCost)}</Text> },
              { label: 'Tempo parado', value: w.downtimeMinutes !== null ? `${formatNumber(Math.round(w.downtimeMinutes / 60))} h` : null },
            ]} />
          </Section>
        )}

        {w.notes && (
          <Section title="Observações">
            <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>{w.notes}</Text>
          </Section>
        )}
      </Stack>

      <StatusModal workOrder={w} target={target} onClose={() => setTarget(null)} />
    </>
  );
}

function ItemsSection({ workOrder: w, canManage }: { workOrder: WorkOrder; canManage: boolean }) {
  const complete = workOrdersApi.useCompleteItem(w.id);
  const canToggle = canManage && (w.status === 'InProgress' || w.status === 'WaitingParts');
  const toggle = (itemId: string, skip: boolean) =>
    complete.mutate({ itemId, skip, notes: null }, { onError: (e) => notifyError(e, 'O item não foi atualizado') });

  return (
    <Section title={`Itens (${w.items.length})`}>
      <Stack gap="xs">
        {w.items.map((item) => (
          <Group key={item.id} justify="space-between" wrap="nowrap" gap="sm">
            <Text size="sm" td={item.status === 'Skipped' ? 'line-through' : undefined}>{item.description}{item.isRequired && <Text span c="red"> *</Text>}</Text>
            <Group gap="xs" wrap="nowrap">
              {canToggle && item.status !== 'Done' && (
                <Button size="compact-xs" variant="light" onClick={() => toggle(item.id, false)}>Concluir</Button>
              )}
              {canToggle && item.status !== 'Skipped' && !item.isRequired && (
                <Button size="compact-xs" variant="subtle" color="gray" onClick={() => toggle(item.id, true)}>Dispensar</Button>
              )}
              <StatusBadge value={item.status} map={WORK_ORDER_ITEM_STATUS} />
            </Group>
          </Group>
        ))}
      </Stack>
    </Section>
  );
}

function PartsSection({ workOrder: w, canManage, canViewCosts }: { workOrder: WorkOrder; canManage: boolean; canViewCosts: boolean }) {
  const [adding, setAdding] = useState(false);
  const addPart = workOrdersApi.useAddPart(w.id);
  const removePart = workOrdersApi.useRemovePart(w.id);
  const editable = canManage && w.status !== 'Completed' && w.status !== 'Cancelled' && w.status !== 'Rejected';

  return (
    <Section title={`Peças (${w.parts.length})`} action={editable && <Button variant="subtle" leftSection={<IconPlus size={16} />} onClick={() => setAdding(true)}>Adicionar peça</Button>}>
      {w.parts.length === 0 ? <Text size="sm" c="dimmed">Nenhuma peça registrada.</Text> : (
        <Table.ScrollContainer minWidth={420}>
          <Table verticalSpacing="xs">
            <Table.Thead><Table.Tr><Table.Th>Peça</Table.Th><Table.Th>Qtd.</Table.Th>{canViewCosts && <><Table.Th>Custo unit.</Table.Th><Table.Th>Total</Table.Th></>}{editable && <Table.Th />}</Table.Tr></Table.Thead>
            <Table.Tbody>
              {w.parts.map((p) => (
                <Table.Tr key={p.id}>
                  <Table.Td><Text size="sm" fw={500}>{p.partName}</Text>{p.supplier && <Text size="xs" c="dimmed">{p.supplier}</Text>}</Table.Td>
                  <Table.Td>{formatNumber(p.quantity)}</Table.Td>
                  {canViewCosts && <><Table.Td>{formatCurrency(p.unitCost)}</Table.Td><Table.Td>{formatCurrency(p.totalCost)}</Table.Td></>}
                  {editable && (
                    <Table.Td>
                      <ActionIcon variant="subtle" color="red" aria-label={`Remover ${p.partName}`} onClick={() => removePart.mutate(p.id, { onError: (e) => notifyError(e, 'A peça não foi removida') })}>
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
      <AddPartModal opened={adding} onClose={() => setAdding(false)}
        onSubmit={(body) => addPart.mutate(body, { onSuccess: () => setAdding(false), onError: (e) => notifyError(e, 'A peça não foi adicionada') })}
        saving={addPart.isPending} />
    </Section>
  );
}

function AddPartModal({ opened, onClose, onSubmit, saving }: {
  opened: boolean; onClose: () => void;
  onSubmit: (body: { partName: string; partNumber: string | null; quantity: number; unitCost: number; supplier: string | null; notes: string | null }) => void;
  saving: boolean;
}) {
  const [partName, setPartName] = useState('');
  const [quantity, setQuantity] = useState<number | ''>(1);
  const [unitCost, setUnitCost] = useState<number | ''>('');
  const [supplier, setSupplier] = useState('');

  const submit = () => onSubmit({ partName, partNumber: null, quantity: Number(quantity), unitCost: Number(unitCost), supplier: supplier || null, notes: null });

  return (
    <Modal opened={opened} onClose={onClose} title="Adicionar peça" centered>
      <Stack>
        <TextInput label="Peça" withAsterisk maxLength={150} value={partName} onChange={(e) => setPartName(e.currentTarget.value)} data-autofocus />
        <SimpleGrid cols={2}>
          <NumberInput label="Quantidade" withAsterisk allowNegative={false} decimalScale={2} value={quantity} onChange={(v) => setQuantity(v === '' ? '' : Number(v))} />
          <NumberInput label="Custo unitário" withAsterisk prefix="R$ " allowNegative={false} decimalScale={2} thousandSeparator="." decimalSeparator=","
            value={unitCost} onChange={(v) => setUnitCost(v === '' ? '' : Number(v))} />
        </SimpleGrid>
        <TextInput label="Fornecedor" maxLength={150} value={supplier} onChange={(e) => setSupplier(e.currentTarget.value)} />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancelar</Button>
          <Button onClick={submit} loading={saving} disabled={!partName.trim() || quantity === '' || unitCost === ''}>Adicionar</Button>
        </Group>
      </Stack>
    </Modal>
  );
}

function LaborSection({ workOrder: w, canManage, canViewCosts }: { workOrder: WorkOrder; canManage: boolean; canViewCosts: boolean }) {
  const [adding, setAdding] = useState(false);
  const addLabor = workOrdersApi.useAddLabor(w.id);
  const removeLabor = workOrdersApi.useRemoveLabor(w.id);
  const editable = canManage && w.status !== 'Completed' && w.status !== 'Cancelled' && w.status !== 'Rejected';

  return (
    <Section title={`Mão de obra (${w.labor.length})`} action={editable && <Button variant="subtle" leftSection={<IconPlus size={16} />} onClick={() => setAdding(true)}>Adicionar mão de obra</Button>}>
      {w.labor.length === 0 ? <Text size="sm" c="dimmed">Nenhuma mão de obra registrada.</Text> : (
        <Table.ScrollContainer minWidth={420}>
          <Table verticalSpacing="xs">
            <Table.Thead><Table.Tr><Table.Th>Técnico</Table.Th><Table.Th>Horas</Table.Th>{canViewCosts && <><Table.Th>Valor/h</Table.Th><Table.Th>Total</Table.Th></>}{editable && <Table.Th />}</Table.Tr></Table.Thead>
            <Table.Tbody>
              {w.labor.map((l) => (
                <Table.Tr key={l.id}>
                  <Table.Td><Text size="sm" fw={500}>{l.technicianName}</Text>{l.description && <Text size="xs" c="dimmed">{l.description}</Text>}</Table.Td>
                  <Table.Td>{formatNumber(l.hours)}</Table.Td>
                  {canViewCosts && <><Table.Td>{formatCurrency(l.hourlyRate)}</Table.Td><Table.Td>{formatCurrency(l.totalCost)}</Table.Td></>}
                  {editable && (
                    <Table.Td>
                      <ActionIcon variant="subtle" color="red" aria-label={`Remover ${l.technicianName}`} onClick={() => removeLabor.mutate(l.id, { onError: (e) => notifyError(e, 'A mão de obra não foi removida') })}>
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
      <AddLaborModal opened={adding} onClose={() => setAdding(false)}
        onSubmit={(body) => addLabor.mutate(body, { onSuccess: () => setAdding(false), onError: (e) => notifyError(e, 'A mão de obra não foi adicionada') })}
        saving={addLabor.isPending} />
    </Section>
  );
}

function AddLaborModal({ opened, onClose, onSubmit, saving }: {
  opened: boolean; onClose: () => void;
  onSubmit: (body: { technicianName: string; hours: number; hourlyRate: number; description: string | null }) => void;
  saving: boolean;
}) {
  const [technicianName, setTechnicianName] = useState('');
  const [hours, setHours] = useState<number | ''>('');
  const [hourlyRate, setHourlyRate] = useState<number | ''>('');
  const [description, setDescription] = useState('');

  const submit = () => onSubmit({ technicianName, hours: Number(hours), hourlyRate: Number(hourlyRate), description: description || null });

  return (
    <Modal opened={opened} onClose={onClose} title="Adicionar mão de obra" centered>
      <Stack>
        <TextInput label="Técnico" withAsterisk maxLength={150} value={technicianName} onChange={(e) => setTechnicianName(e.currentTarget.value)} data-autofocus />
        <SimpleGrid cols={2}>
          <NumberInput label="Horas" withAsterisk allowNegative={false} decimalScale={2} value={hours} onChange={(v) => setHours(v === '' ? '' : Number(v))} />
          <NumberInput label="Valor da hora" withAsterisk prefix="R$ " allowNegative={false} decimalScale={2} thousandSeparator="." decimalSeparator=","
            value={hourlyRate} onChange={(v) => setHourlyRate(v === '' ? '' : Number(v))} />
        </SimpleGrid>
        <TextInput label="Descrição" maxLength={500} value={description} onChange={(e) => setDescription(e.currentTarget.value)} />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancelar</Button>
          <Button onClick={submit} loading={saving} disabled={!technicianName.trim() || hours === '' || hourlyRate === ''}>Adicionar</Button>
        </Group>
      </Stack>
    </Modal>
  );
}

function StatusModal({ workOrder: w, target, onClose }: { workOrder: WorkOrder; target: WorkOrderStatus | null; onClose: () => void }) {
  const [text, setText] = useState('');
  const [odometerKm, setOdometerKm] = useState<number | ''>('');
  const change = workOrdersApi.useChangeStatus(w.id);
  const completing = target === 'Completed';
  const cancelling = target === 'Cancelled' || target === 'Rejected';

  const submit = () =>
    change.mutate({
      status: target!, resolution: completing ? text : null, reason: cancelling ? text : null,
      odometerKm: completing && odometerKm !== '' ? Number(odometerKm) : null, hourMeter: null,
    }, {
      onSuccess: () => { notifySuccess(`Ordem de serviço ${WORK_ORDER_STATUS[target!].label.toLowerCase()}.`); setText(''); setOdometerKm(''); onClose(); },
      onError: (e) => notifyError(e, 'A situação não foi alterada'),
    });

  return (
    <Modal opened={!!target} onClose={onClose} title={target ? WORK_ORDER_ACTION[target] : ''} centered>
      <Stack>
        {completing && (
          <>
            <Textarea label="Resolução" withAsterisk autosize minRows={3} maxLength={2000} value={text} onChange={(e) => setText(e.currentTarget.value)} data-autofocus />
            <NumberInput label="Hodômetro no fechamento (opcional)" suffix=" km" allowNegative={false} allowDecimal={false} thousandSeparator="." decimalSeparator=","
              value={odometerKm} onChange={(v) => setOdometerKm(v === '' ? '' : Number(v))} />
          </>
        )}
        {cancelling && (
          <Textarea label={target === 'Rejected' ? 'Motivo da rejeição' : 'Motivo do cancelamento'} withAsterisk autosize minRows={3} maxLength={2000}
            value={text} onChange={(e) => setText(e.currentTarget.value)} data-autofocus />
        )}
        {!completing && !cancelling && <Text size="sm">A ordem de serviço passará para "{target && WORK_ORDER_STATUS[target].label}".</Text>}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Voltar</Button>
          <Button color={cancelling ? 'red' : undefined} onClick={submit} loading={change.isPending} disabled={(completing || cancelling) && !text.trim()}>
            {target ? WORK_ORDER_ACTION[target] : ''}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
