import { useState } from 'react';
import { Button, Group, Modal, NumberInput, Paper, Select, SimpleGrid, Stack, Table, Text, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconPlus } from '@tabler/icons-react';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { RowActions } from '../../components/common';
import { VehiclePicker } from '../../components/EntityPickers';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { formatCurrency } from '../../lib/format';
import { budgetsApi, costCentersApi, expenseCategoriesApi } from './api';
import { BudgetProgress, BudgetStatusBadge, PartialTotalsNotice } from './components';
import type { Budget } from './finance';

const MONTHS = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];
const currentYear = new Date().getFullYear();

export function BudgetsPage() {
  const { can } = useAuth();
  const [year, setYear] = useState(currentYear);
  const budgets = budgetsApi.useList(year);
  const vsActual = budgetsApi.useVsActual(year, undefined);
  const removeMutation = budgetsApi.useRemove();
  const [editing, setEditing] = useState<Budget | 'new' | null>(null);
  const canManage = can(PERMISSIONS.finance.managebudgets);
  const canSeeCosts = can(PERMISSIONS.finance.viewcosts);

  const newButton = canManage && <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing('new')}>Novo orçamento</Button>;
  const yearOptions = Array.from({ length: 5 }, (_, i) => String(currentYear - 2 + i));

  if (budgets.error && !budgets.data) return <><PageHeader title="Orçamentos" action={newButton} /><Paper><ErrorState error={budgets.error} onRetry={() => void budgets.refetch()} /></Paper></>;
  const items = budgets.data ?? [];
  const vsActualItems = vsActual.data ?? [];
  const isPartial = vsActualItems.some((v) => v.isPartial);

  return (
    <>
      <PageHeader title="Orçamentos" description="Valor planejado por categoria, ano (ou mês) e, opcionalmente, centro de custo ou veículo." action={newButton} />
      <Select label="Ano" data={yearOptions} value={String(year)} onChange={(v) => setYear(Number(v))} w={160} mb="md" />

      {items.length === 0 ? (
        <Paper><EmptyState icon={<IconPlus size={28} />} title={`Nenhum orçamento em ${year}`} description="Cadastre o valor planejado por categoria para acompanhar o orçado x realizado." action={newButton} /></Paper>
      ) : (
        <Stack gap="xl">
          <Paper>
            <Table.ScrollContainer minWidth={560}>
              <Table highlightOnHover>
                <Table.Thead>
                  <Table.Tr><Table.Th>Categoria</Table.Th><Table.Th>Período</Table.Th><Table.Th>Escopo</Table.Th>
                    {canSeeCosts && <Table.Th ta="right">Valor orçado</Table.Th>}<Table.Th style={{ width: 56 }} /></Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {items.map((b) => (
                    <Table.Tr key={b.id}>
                      <Table.Td>{b.categoryName}</Table.Td>
                      <Table.Td>{b.month ? MONTHS[b.month - 1] : 'Ano todo'}</Table.Td>
                      <Table.Td>{b.licensePlate ?? b.costCenterName ?? 'Frota'}</Table.Td>
                      {canSeeCosts && <Table.Td ta="right">{formatCurrency(b.amount)}</Table.Td>}
                      <Table.Td>
                        {canManage && (
                          <RowActions onEdit={() => setEditing(b)} onDelete={() => confirmDelete({
                            entity: 'o orçamento', name: `${b.categoryName} · ${year}`, onConfirm: () => removeMutation.mutate(b.id, {
                              onError: (e) => notifyError(e, 'O orçamento não foi excluído'),
                              onSuccess: () => notifySuccess('Orçamento excluído.'),
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

          {canSeeCosts && (
            <Stack gap="sm">
              <Text fw={650}>Orçado x Realizado — {year}</Text>
              {isPartial && <PartialTotalsNotice />}
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
                {vsActualItems.map((v) => (
                  <Paper key={v.budget.id} p="md">
                    <Group justify="space-between" mb="xs">
                      <Text fw={600}>{v.budget.categoryName}</Text>
                      <BudgetStatusBadge value={v.status} />
                    </Group>
                    <BudgetProgress utilizationPercent={v.utilizationPercent} status={v.status} />
                    <Group justify="space-between" mt="xs">
                      <Text size="sm" c="dimmed">Orçado: {formatCurrency(v.budget.amount)}</Text>
                      <Text size="sm" c="dimmed">Realizado: {formatCurrency(v.actual)}</Text>
                    </Group>
                    {v.remaining !== null && <Text size="sm" c="dimmed">Restante: {formatCurrency(v.remaining)}</Text>}
                  </Paper>
                ))}
              </SimpleGrid>
            </Stack>
          )}
        </Stack>
      )}
      {editing && <BudgetModal budget={editing === 'new' ? null : editing} year={year} onClose={() => setEditing(null)} />}
    </>
  );
}

function BudgetModal({ budget, year, onClose }: { budget: Budget | null; year: number; onClose: () => void }) {
  const mutation = budgetsApi.useSave(budget?.id);
  const categories = expenseCategoriesApi.useList(false).data ?? [];
  const costCenters = costCentersApi.useList(false).data ?? [];
  const initialValues = {
    year: budget?.year ?? year, month: budget?.month ?? null, expenseCategoryId: budget?.expenseCategoryId ?? null,
    costCenterId: budget?.costCenterId ?? null, vehicleId: budget?.vehicleId ?? null,
    amount: (budget?.amount ?? '') as number | string, notes: budget?.notes ?? '',
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validate: {
      expenseCategoryId: (v) => (v ? null : 'Categoria: campo obrigatório.'),
      amount: (v) => (v === '' || Number(v) <= 0 ? 'O valor orçado deve ser maior que zero.' : null),
    },
  });

  return (
    <Modal opened onClose={onClose} title={budget ? 'Editar orçamento' : 'Novo orçamento'} centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate(
        {
          year: v.year, month: v.month ? Number(v.month) : null, expenseCategoryId: v.expenseCategoryId, costCenterId: v.costCenterId,
          vehicleId: v.vehicleId, amount: Number(v.amount), notes: v.notes.trim() || null,
        },
        { onSuccess: () => { notifySuccess('Orçamento salvo com sucesso.'); onClose(); }, onError: (e) => { const err = notifyError(e, 'O orçamento não foi salvo'); form.setErrors(err.fieldErrors); } },
      ))}>
        <Stack gap="md">
          <Group grow>
            <NumberInput label="Ano" withAsterisk min={2000} max={2100} {...form.getInputProps('year')} />
            <Select label="Mês (em branco = ano todo)" data={MONTHS.map((m, i) => ({ value: String(i + 1), label: m }))} clearable allowDeselect
              value={form.values.month ? String(form.values.month) : null} onChange={(v) => form.setFieldValue('month', v ? Number(v) : null)} />
          </Group>
          <Select label="Categoria" withAsterisk data={categories.map((c) => ({ value: c.id, label: c.name }))} searchable {...form.getInputProps('expenseCategoryId')} />
          <Select label="Centro de custo (opcional)" data={costCenters.map((c) => ({ value: c.id, label: c.name }))} clearable allowDeselect {...form.getInputProps('costCenterId')} />
          <VehiclePicker label="Veículo (opcional)" value={form.values.vehicleId} initialLabel={budget?.licensePlate} onChange={(v) => form.setFieldValue('vehicleId', v)} />
          <NumberInput label="Valor orçado" withAsterisk prefix="R$ " decimalSeparator="," thousandSeparator="." decimalScale={2} allowNegative={false} {...form.getInputProps('amount')} />
          <Textarea label="Observações" autosize minRows={2} {...form.getInputProps('notes')} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>Salvar</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
