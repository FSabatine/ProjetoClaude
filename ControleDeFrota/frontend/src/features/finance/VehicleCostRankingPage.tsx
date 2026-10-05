import { Paper, Select, Stack, Text } from '@mantine/core';
import { IconTrophy } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { DataTable, type Column } from '../../components/DataTable';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatCurrency, formatPlate } from '../../lib/format';
import { usePeriod, PeriodFilter } from '../fuel/components';
import { financeAnalyticsApi } from './api';
import type { VehicleRankingRow } from './finance';

const FILTERS = ['type', 'status'] as const;

const VEHICLE_TYPE = { Light: 'Leve', Medium: 'Médio', SemiHeavy: 'Semipesado', Heavy: 'Pesado' };
const VEHICLE_STATUS = { Available: 'Disponível', OnTrip: 'Em viagem', Unavailable: 'Indisponível', UnderMaintenance: 'Em manutenção', Inactive: 'Inativo' };

export function VehicleCostRankingPage() {
  const navigate = useNavigate();
  const list = useListParams(FILTERS, { by: 'totalcost', direction: 'Desc' });
  const period = usePeriod(30);
  const query = financeAnalyticsApi.useRanking({ ...list.apiParams, from: period.from, to: period.to });
  const f = list.filters;

  const columns: Column<VehicleRankingRow>[] = [
    { key: 'vehicle', header: 'Veículo', render: (r) => <Text size="sm" fw={600} ff="monospace">{formatPlate(r.licensePlate)}</Text> },
    { key: 'model', header: 'Modelo', secondary: true, render: (r) => r.model },
    { key: 'totalCost', header: 'Custo total', sortKey: 'totalcost', align: 'right', render: (r) => formatCurrency(r.totalCost) },
    { key: 'costPerKm', header: 'Custo/km', sortKey: 'costperkm', align: 'right', render: (r) => r.costPerKm === null ? <Text size="xs" c="dimmed">dados insuficientes</Text> : formatCurrency(r.costPerKm) },
    { key: 'fuelCost', header: 'Combustível', sortKey: 'fuelcost', align: 'right', secondary: true, render: (r) => formatCurrency(r.fuelCost) },
    { key: 'maintenanceCost', header: 'Manutenção', sortKey: 'maintenancecost', align: 'right', secondary: true, render: (r) => formatCurrency(r.maintenanceCost) },
    { key: 'tireCost', header: 'Pneus', sortKey: 'tirecost', align: 'right', secondary: true, render: (r) => formatCurrency(r.tireCost) },
    { key: 'expenseCount', header: 'Nº despesas', sortKey: 'expensecount', align: 'right', render: (r) => r.expenseCount },
  ];

  return (
    <>
      <PageHeader title="Ranking de veículos" description="Veículos ordenados por custo no período — encontre rapidamente os mais caros da frota." />
      <PeriodFilter period={period} />
      <Paper p="sm" mb="md" mt={-8}>
        <Select placeholder="Tipo de veículo" data={Object.entries(VEHICLE_TYPE).map(([value, label]) => ({ value, label }))}
          clearable allowDeselect value={f.type} onChange={(v) => list.setFilter('type', v)} w={200} display="inline-block" mr="sm" />
        <Select placeholder="Situação" data={Object.entries(VEHICLE_STATUS).map(([value, label]) => ({ value, label }))}
          clearable allowDeselect value={f.status} onChange={(v) => list.setFilter('status', v)} w={200} display="inline-block" />
      </Paper>
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(r) => r.vehicleId}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(r) => navigate(`/veiculos/${r.vehicleId}?aba=financeiro`)}
        renderCard={(r) => (
          <Stack gap={4}>
            <Text fw={700} ff="monospace">{formatPlate(r.licensePlate)}</Text>
            <Text size="sm">{r.model}</Text>
            <Text size="sm">Custo total: {formatCurrency(r.totalCost)} · Custo/km: {r.costPerKm === null ? '—' : formatCurrency(r.costPerKm)}</Text>
          </Stack>
        )}
        emptyState={<EmptyState icon={<IconTrophy size={28} />} title="Nenhum custo no período" description="Ajuste o período para ver o ranking de custos por veículo." />}
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
