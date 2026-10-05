import { useState } from 'react';
import { Badge, Button, Group, Modal, NumberInput, Paper, Select, Stack, Switch, Table, TextInput } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { IconPlus, IconRepeat } from '@tabler/icons-react';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { toSelectData, RowActions } from '../../components/common';
import { VehiclePicker, WorkshopPicker } from '../../components/EntityPickers';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { formatCurrency, formatDate } from '../../lib/format';
import { expenseCategoriesApi, recurringExpensesApi } from './api';
import { EXPENSE_FREQUENCY, type RecurringExpense } from './finance';

export function RecurringExpensesPage() {
  const { can } = useAuth();
  const query = recurringExpensesApi.useList(true);
  const removeMutation = recurringExpensesApi.useRemove();
  const [editing, setEditing] = useState<RecurringExpense | 'new' | null>(null);
  const canManage = can(PERMISSIONS.finance.managerecurring);
  const canSeeCosts = can(PERMISSIONS.finance.viewcosts);

  const newButton = canManage && <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing('new')}>Nova despesa recorrente</Button>;

  if (query.error && !query.data) return <><PageHeader title="Despesas recorrentes" action={newButton} /><Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper></>;
  const items = query.data ?? [];

  return (
    <>
      <PageHeader
        title="Despesas recorrentes"
        description="Seguro, financiamento, leasing, assinaturas… cadastradas uma vez e geradas automaticamente até 30 dias antes de cada vencimento."
        action={newButton} />
      {items.length === 0 ? (
        <Paper><EmptyState icon={<IconRepeat size={28} />} title="Nenhuma despesa recorrente" description="Cadastre uma obrigação periódica para que as despesas sejam geradas sozinhas." action={newButton} /></Paper>
      ) : (
        <Paper>
          <Table.ScrollContainer minWidth={640}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr><Table.Th>Descrição</Table.Th><Table.Th>Categoria</Table.Th><Table.Th>Frequência</Table.Th>
                  {canSeeCosts && <Table.Th ta="right">Valor</Table.Th>}<Table.Th>Próximo vencimento</Table.Th><Table.Th>Situação</Table.Th><Table.Th style={{ width: 56 }} /></Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {items.map((r) => (
                  <Table.Tr key={r.id}>
                    <Table.Td>{r.description}</Table.Td>
                    <Table.Td>{r.categoryName}</Table.Td>
                    <Table.Td>{EXPENSE_FREQUENCY[r.frequency].label}</Table.Td>
                    {canSeeCosts && <Table.Td ta="right">{formatCurrency(r.amount)}</Table.Td>}
                    <Table.Td>{formatDate(r.nextDueDate)}</Table.Td>
                    <Table.Td>{r.isActive ? <Badge color="teal">Ativa</Badge> : <Badge color="gray">Inativa</Badge>}</Table.Td>
                    <Table.Td>
                      {canManage && (
                        <RowActions onEdit={() => setEditing(r)} onDelete={() => confirmDelete({
                          entity: 'a despesa recorrente', name: r.description, onConfirm: () => removeMutation.mutate(r.id, {
                            onError: (e) => notifyError(e, 'A despesa recorrente não foi excluída'),
                            onSuccess: () => notifySuccess('Despesa recorrente excluída.'),
                          }),
                        })} />
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Paper>
      )}
      {editing && <RecurringModal recurring={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  );
}

function RecurringModal({ recurring, onClose }: { recurring: RecurringExpense | null; onClose: () => void }) {
  const mutation = recurringExpensesApi.useSave(recurring?.id);
  const categories = (expenseCategoriesApi.useList(false).data ?? []).filter((c) => !c.isSystemCategory);
  const initialValues = {
    description: recurring?.description ?? '', expenseCategoryId: recurring?.expenseCategoryId ?? null,
    vehicleId: recurring?.vehicleId ?? null, workshopId: recurring?.workshopId ?? null, supplierName: recurring?.supplierName ?? '',
    amount: (recurring?.amount ?? '') as number | string, frequency: recurring?.frequency ?? 'Monthly',
    startDate: recurring ? new Date(recurring.startDate) : new Date(), endDate: recurring?.endDate ? new Date(recurring.endDate) : null,
    dueDayOfMonth: recurring?.dueDayOfMonth ?? 10, isActive: recurring?.isActive ?? true,
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validate: {
      description: (v) => (v.trim() ? null : 'Descrição: campo obrigatório.'),
      expenseCategoryId: (v) => (v ? null : 'Categoria: campo obrigatório.'),
      amount: (v) => (v === '' || Number(v) <= 0 ? 'O valor deve ser maior que zero.' : null),
      dueDayOfMonth: (v) => (v >= 1 && v <= 28 ? null : 'Dia do vencimento deve ser entre 1 e 28.'),
    },
  });
  const toIso = (d: Date | null) => d ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` : null;

  return (
    <Modal opened onClose={onClose} title={recurring ? 'Editar despesa recorrente' : 'Nova despesa recorrente'} size="lg" centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate(
        {
          description: v.description.trim(), expenseCategoryId: v.expenseCategoryId, vehicleId: v.vehicleId, workshopId: v.workshopId,
          supplierName: v.supplierName.trim() || null, amount: Number(v.amount), frequency: v.frequency, startDate: toIso(v.startDate),
          endDate: toIso(v.endDate), dueDayOfMonth: v.dueDayOfMonth, isActive: v.isActive, costCenterId: recurring?.costCenterId ?? null, paymentMethod: null,
        },
        { onSuccess: () => { notifySuccess('Despesa recorrente salva com sucesso.'); onClose(); }, onError: (e) => { const err = notifyError(e, 'Não foi possível salvar'); form.setErrors(err.fieldErrors); } },
      ))}>
        <Stack gap="md">
          <TextInput label="Descrição" withAsterisk {...form.getInputProps('description')} />
          <Select label="Categoria" withAsterisk data={categories.map((c) => ({ value: c.id, label: c.name }))} searchable {...form.getInputProps('expenseCategoryId')} />
          <Group grow>
            <NumberInput label="Valor" withAsterisk prefix="R$ " decimalSeparator="," thousandSeparator="." decimalScale={2} allowNegative={false} {...form.getInputProps('amount')} />
            <Select label="Frequência" withAsterisk data={toSelectData(EXPENSE_FREQUENCY)} {...form.getInputProps('frequency')} />
          </Group>
          <Group grow>
            <DateInput label="Início" withAsterisk valueFormat="DD/MM/YYYY" {...form.getInputProps('startDate')} />
            <DateInput label="Término (opcional)" valueFormat="DD/MM/YYYY" clearable {...form.getInputProps('endDate')} />
            <NumberInput label="Dia do vencimento" withAsterisk min={1} max={28} {...form.getInputProps('dueDayOfMonth')} />
          </Group>
          <VehiclePicker label="Veículo (opcional)" value={form.values.vehicleId} initialLabel={recurring?.licensePlate} onChange={(v) => form.setFieldValue('vehicleId', v)} />
          <WorkshopPicker label="Fornecedor cadastrado (opcional)" value={form.values.workshopId} initialLabel={recurring?.workshopName} onChange={(v) => form.setFieldValue('workshopId', v)} />
          <TextInput label="Nome do fornecedor (se não cadastrado)" {...form.getInputProps('supplierName')} />
          <Switch label="Ativa" {...form.getInputProps('isActive', { type: 'checkbox' })} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>Salvar</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
