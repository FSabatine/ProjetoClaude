import { Button, Group, Paper, Stack, Text } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconFileInvoice, IconPlus } from '@tabler/icons-react';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { VehiclePicker } from '../../components/EntityPickers';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatCurrency, formatDate, formatPlate } from '../../lib/format';
import { expenseCategoriesApi, expensesApi } from './api';
import { PaymentStatusBadge } from './components';
import { PAYMENT_STATUS, type Expense } from './finance';

const FILTERS = ['status', 'expenseCategoryId', 'vehicleId', 'from', 'to'] as const;

export function ExpensesListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'expenseDate', direction: 'Desc' });
  const query = expensesApi.useList(list.apiParams);
  const categories = expenseCategoriesApi.useList(false);
  const canSeeCosts = can(PERMISSIONS.finance.viewcosts);
  const f = list.filters;

  const newButton = can(PERMISSIONS.finance.create) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/financeiro/despesas/novo')}>Nova despesa</Button>
  );

  const columns: Column<Expense>[] = [
    { key: 'expenseDate', header: 'Data', sortKey: 'expenseDate', render: (r) => formatDate(r.expenseDate) },
    { key: 'description', header: 'Descrição', render: (r) => (
      <Stack gap={0}>
        <Text size="sm">{r.description}</Text>
        {r.isDuplicateSuspect && <Text size="xs" c="orange">possível duplicidade</Text>}
      </Stack>
    ) },
    { key: 'category', header: 'Categoria', secondary: true, render: (r) => r.categoryName },
    { key: 'vehicle', header: 'Veículo', secondary: true, render: (r) => r.licensePlate ? formatPlate(r.licensePlate) : '—' },
    { key: 'dueDate', header: 'Vencimento', sortKey: 'dueDate', secondary: true, render: (r) => formatDate(r.dueDate) },
    ...(canSeeCosts ? [{ key: 'amount', header: 'Valor', sortKey: 'amount', align: 'right' as const, render: (r: Expense) => formatCurrency(r.amount) }] : []),
    { key: 'status', header: 'Situação', render: (r) => <PaymentStatusBadge value={r.status} /> },
  ];

  return (
    <>
      <PageHeader title="Despesas" description="Despesas manuais da frota (seguro, IPVA, pedágio, multas…). Combustível, manutenção e pneus vêm dos próprios módulos." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por descrição ou referência"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(PAYMENT_STATUS), value: f.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'expenseCategoryId', placeholder: 'Categoria', data: (categories.data ?? []).map((c) => ({ value: c.id, label: c.name })), value: f.expenseCategoryId, onChange: (v) => list.setFilter('expenseCategoryId', v) },
        ]}
      />
      <Paper p="sm" mb="md" mt={-8}>
        <Group gap="sm" wrap="wrap">
          <VehiclePicker aria-label="Veículo" placeholder="Veículo" value={f.vehicleId} onChange={(v) => list.setFilter('vehicleId', v)} style={{ flex: '1 1 220px' }} />
          <DatePickerInput type="range" aria-label="Período" placeholder="Período" valueFormat="DD/MM/YYYY" clearable
            value={[f.from ? dayjs(f.from).toDate() : null, f.to ? dayjs(f.to).toDate() : null]}
            onChange={([start, end]) => {
              list.setFilter('from', start ? dayjs(start).format('YYYY-MM-DD') : null);
              if (!start || end) list.setFilter('to', end ? dayjs(end).format('YYYY-MM-DD') : null);
            }}
            style={{ flex: '1 1 220px', maxWidth: 280 }} />
        </Group>
      </Paper>
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(r) => r.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(r) => navigate(`/financeiro/despesas/${r.id}/editar`)}
        renderCard={(r) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={700}>{r.description}</Text>
              <PaymentStatusBadge value={r.status} />
            </Group>
            <Text size="sm">{r.categoryName}{r.licensePlate ? ` · ${formatPlate(r.licensePlate)}` : ''}{canSeeCosts ? ` · ${formatCurrency(r.amount)}` : ''}</Text>
            <Text size="xs" c="dimmed">{formatDate(r.expenseDate)}{r.dueDate ? ` · vence ${formatDate(r.dueDate)}` : ''}</Text>
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconFileInvoice size={28} />} title="Nenhuma despesa registrada"
            description="Registre despesas como seguro, IPVA, pedágio ou multas. Combustível, manutenção e pneus já aparecem automaticamente nos relatórios." action={newButton} />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
