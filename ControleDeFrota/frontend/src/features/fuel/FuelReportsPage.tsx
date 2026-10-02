import { useState } from 'react';
import { Anchor, Group, Paper, SegmentedControl, Tabs, Text } from '@mantine/core';
import { Link, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { PageHeader } from '../../components/PageHeader';
import { ReportTable, useReportState } from '../../components/ReportTable';
import { formatCurrency, formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { formatConsumption, formatDeviation, formatQuantity, formatUnitPrice } from '../../lib/fuel';
import { fuelAnalyticsApi, fuelingsApi } from './api';
import { InfoHint, PeriodFilter, usePeriod, CONSUMPTION_HELP } from './components';
import { COST_GROUPING, FUELING_STATUS, type CostGrouping, type CostReportRow, type StationReportRow } from './fuel';

const MONTHS = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

/** /combustivel/relatorios — the four reports of seção 32 plus price over time, with period, sort and pagination. */
export function FuelReportsPage() {
  const { can } = useAuth();
  const canSeeCosts = can(PERMISSIONS.fuel.viewcosts);
  const period = usePeriod(30);
  const [params, setParams] = useSearchParams();
  const tabs = ['abastecimentos', 'consumo', ...(canSeeCosts ? ['custos'] : []), 'postos', ...(canSeeCosts ? ['precos'] : [])];
  const tab = tabs.includes(params.get('relatorio') ?? '') ? params.get('relatorio')! : 'abastecimentos';

  return (
    <>
      <PageHeader title="Relatórios de combustível" description="Valores operacionais do período (não são valores contábeis)."
        breadcrumbs={[{ label: 'Combustível', to: '/combustivel' }, { label: 'Relatórios' }]} />
      <PeriodFilter period={period} />
      <Tabs value={tab} keepMounted={false}
        onChange={(v) => setParams((p) => { const n = new URLSearchParams(p); n.set('relatorio', v ?? 'abastecimentos'); return n; }, { replace: true })}>
        <Tabs.List mb="md">
          <Tabs.Tab value="abastecimentos">Abastecimentos</Tabs.Tab>
          <Tabs.Tab value="consumo">Consumo</Tabs.Tab>
          {canSeeCosts && <Tabs.Tab value="custos">Custos</Tabs.Tab>}
          <Tabs.Tab value="postos">Postos</Tabs.Tab>
          {canSeeCosts && <Tabs.Tab value="precos">Preços</Tabs.Tab>}
        </Tabs.List>
        <Paper>
          <Tabs.Panel value="abastecimentos"><FuelingReport from={period.from} to={period.to} /></Tabs.Panel>
          <Tabs.Panel value="consumo"><ConsumptionReport from={period.from} to={period.to} /></Tabs.Panel>
          {canSeeCosts && <Tabs.Panel value="custos"><CostReport from={period.from} to={period.to} /></Tabs.Panel>}
          <Tabs.Panel value="postos"><StationReport from={period.from} to={period.to} /></Tabs.Panel>
          {canSeeCosts && <Tabs.Panel value="precos"><PriceReport from={period.from} to={period.to} /></Tabs.Panel>}
        </Paper>
      </Tabs>
    </>
  );
}

function FuelingReport({ from, to }: { from: string; to: string }) {
  const state = useReportState('fueledAt', 'Desc');
  const query = fuelingsApi.useList({ from, to, ...state.params });
  return (
    <>
      <Text size="sm" c="dimmed" px="md" pt="sm">
        Para filtrar por veículo, motorista, posto ou alerta, use a <Anchor component={Link} to={`/abastecimentos?from=${from}&to=${to}`}>lista de abastecimentos</Anchor>.
      </Text>
      <ReportTable state={state} data={query.data} isLoading={query.isFetching} error={query.error} onRetry={() => void query.refetch()} getKey={(r) => r.id}
        empty="Nenhum abastecimento no período."
        columns={[
          { key: 'date', header: 'Data', sortKey: 'fueledAt', render: (r) => <Anchor component={Link} to={`/abastecimentos/${r.id}`} size="sm">{formatDateTime(r.fueledAt)}</Anchor> },
          { key: 'vehicle', header: 'Veículo', sortKey: 'licensePlate', render: (r) => <Text span ff="monospace" size="sm">{formatPlate(r.licensePlate)}</Text> },
          { key: 'driver', header: 'Motorista', render: (r) => r.driverName ?? '—' },
          { key: 'station', header: 'Posto', render: (r) => r.fuelStationName ?? '—' },
          { key: 'fuel', header: 'Combustível', render: (r) => r.fuelTypeName },
          { key: 'quantity', header: 'Quantidade', sortKey: 'quantity', align: 'right', render: (r) => formatQuantity(r.quantity, r.unit) },
          { key: 'price', header: 'Preço', align: 'right', render: (r) => formatUnitPrice(r.unitPrice, r.unit) },
          { key: 'total', header: 'Total', align: 'right', render: (r) => formatCurrency(r.totalAmount) },
          { key: 'odometer', header: 'Hodômetro', sortKey: 'odometerKm', align: 'right', render: (r) => `${formatNumber(r.odometerKm)} km` },
          { key: 'status', header: 'Situação', render: (r) => <StatusBadge value={r.status} map={FUELING_STATUS} /> },
        ]} />
    </>
  );
}

function ConsumptionReport({ from, to }: { from: string; to: string }) {
  const state = useReportState('licensePlate');
  const query = fuelAnalyticsApi.useConsumption({ from, to, ...state.params });
  return (
    <>
      <Group gap={4} px="md" pt="sm">
        <Text size="sm" c="dimmed">Só trechos medidos (tanque cheio a tanque cheio) fechados no período. O esperado usa a referência de cada trecho no momento do cálculo.</Text>
        <InfoHint label="Como o consumo é calculado">{CONSUMPTION_HELP}</InfoHint>
      </Group>
      <ReportTable state={state} data={query.data?.rows} isLoading={query.isFetching} error={query.error} onRetry={() => void query.refetch()}
        getKey={(r) => `${r.vehicleId}-${r.unit}`} empty="Nenhum trecho de consumo medido no período."
        columns={[
          { key: 'vehicle', header: 'Veículo', sortKey: 'licensePlate', render: (r) => <Anchor component={Link} to={`/veiculos/${r.vehicleId}?aba=combustivel`} ff="monospace" size="sm">{formatPlate(r.licensePlate)}</Anchor> },
          { key: 'segments', header: 'Trechos', align: 'right', render: (r) => r.segments },
          { key: 'distance', header: 'Distância', sortKey: 'distanceKm', align: 'right', render: (r) => `${formatNumber(r.distanceKm)} km` },
          { key: 'fuel', header: 'Combustível', align: 'right', render: (r) => formatQuantity(r.quantity, r.unit) },
          { key: 'consumption', header: 'Consumo', sortKey: 'consumption', align: 'right', render: (r) => formatConsumption(r.consumption, r.unit) },
          { key: 'expected', header: 'Esperado', align: 'right', render: (r) => formatConsumption(r.expectedConsumption, r.unit) },
          { key: 'variance', header: 'Variação', sortKey: 'variancePercent', align: 'right', render: (r) => formatDeviation(r.variancePercent) },
        ]} />
    </>
  );
}

function CostReport({ from, to }: { from: string; to: string }) {
  const [groupBy, setGroupBy] = useState<CostGrouping>('Vehicle');
  const state = useReportState('cost', 'Desc');
  const query = fuelAnalyticsApi.useCosts({ from, to, groupBy, ...state.params });
  const byVehicle = groupBy === 'Vehicle';
  return (
    <>
      <Group px="md" pt="sm" justify="space-between" gap="sm">
        <SegmentedControl value={groupBy} onChange={(v) => { setGroupBy(v as CostGrouping); state.setPage(1); }}
          data={Object.entries(COST_GROUPING).map(([value, { label }]) => ({ value, label }))} />
        <Text size="xs" c="dimmed">Custo/km usa só os trechos com consumo medido.</Text>
      </Group>
      <ReportTable state={state} data={query.data?.rows} isLoading={query.isFetching} error={query.error} onRetry={() => void query.refetch()}
        getKey={(r) => r.key ?? r.name} empty="Nenhum abastecimento no período."
        columns={[
          { key: 'name', header: COST_GROUPING[groupBy].label.replace('Por ', '').replace(/^./, (c) => c.toUpperCase()), sortKey: 'name',
            render: (r) => byVehicle && r.key ? <Anchor component={Link} to={`/veiculos/${r.key}?aba=combustivel`} ff="monospace" size="sm">{formatPlate(r.name)}</Anchor> : r.name },
          { key: 'fuelings', header: 'Abastecimentos', sortKey: 'fuelings', align: 'right', render: (r) => r.fuelings },
          { key: 'liters', header: 'Litros', align: 'right', render: (r) => formatQuantity(r.liters) },
          { key: 'cost', header: 'Custo', sortKey: 'cost', align: 'right', render: (r) => formatCurrency(r.cost) },
          ...(byVehicle ? [
            { key: 'distance', header: 'Distância medida', align: 'right' as const, render: (r: CostReportRow) => r.distanceKm ? `${formatNumber(r.distanceKm)} km` : '—' },
            { key: 'costPerKm', header: 'Custo/km', sortKey: 'costPerKm', align: 'right' as const, render: (r: CostReportRow) => formatCurrency(r.costPerKm) },
          ] : []),
        ]} />
    </>
  );
}

function StationReport({ from, to }: { from: string; to: string }) {
  const state = useReportState('name');
  const query = fuelAnalyticsApi.useStations({ from, to, ...state.params });
  const costs = query.data?.canSeeCosts ?? false;
  return (
    <ReportTable state={state} data={query.data?.rows} isLoading={query.isFetching} error={query.error} onRetry={() => void query.refetch()}
      getKey={(r) => `${r.fuelStationId}-${r.fuelTypeId}`} empty="Nenhum abastecimento no período."
      columns={[
        { key: 'station', header: 'Posto', sortKey: 'name', render: (r) => r.fuelStationId ? <Anchor component={Link} to={`/postos/${r.fuelStationId}`} size="sm">{r.stationName}</Anchor> : r.stationName },
        { key: 'fuel', header: 'Combustível', render: (r) => r.fuelTypeName },
        { key: 'fuelings', header: 'Abastecimentos', sortKey: 'fuelings', align: 'right', render: (r) => r.fuelings },
        { key: 'quantity', header: 'Quantidade', sortKey: 'quantity', align: 'right', render: (r) => formatQuantity(r.quantity, r.unit) },
        ...(costs ? [
          { key: 'avg', header: 'Preço médio', sortKey: 'averagePrice', align: 'right' as const, render: (r: StationReportRow) => formatUnitPrice(r.averagePrice, r.unit) },
          { key: 'range', header: 'Mín. – máx.', align: 'right' as const, render: (r: StationReportRow) => `${formatCurrency(r.minPrice)} – ${formatCurrency(r.maxPrice)}` },
          { key: 'total', header: 'Custo total', sortKey: 'totalCost', align: 'right' as const, render: (r: StationReportRow) => formatCurrency(r.totalCost) },
        ] : []),
      ]} />
  );
}

function PriceReport({ from, to }: { from: string; to: string }) {
  const state = useReportState('month');
  const query = fuelAnalyticsApi.usePrices({ from, to, ...state.params });
  return (
    <>
      <Text size="sm" c="dimmed" px="md" pt="sm">Preço médio ponderado: total pago ÷ quantidade, por combustível e mês.</Text>
      <ReportTable state={state} data={query.data?.rows} isLoading={query.isFetching} error={query.error} onRetry={() => void query.refetch()}
        getKey={(r) => `${r.year}-${r.month}-${r.fuelTypeId}`} empty="Nenhum abastecimento no período."
        columns={[
          { key: 'month', header: 'Mês', render: (r) => `${MONTHS[r.month - 1]}/${r.year}` },
          { key: 'fuel', header: 'Combustível', render: (r) => r.fuelTypeName },
          { key: 'fuelings', header: 'Abastecimentos', align: 'right', render: (r) => r.fuelings },
          { key: 'avg', header: 'Preço médio', align: 'right', render: (r) => formatUnitPrice(r.averagePrice, r.unit) },
          { key: 'min', header: 'Mínimo', align: 'right', render: (r) => formatUnitPrice(r.minPrice, r.unit) },
          { key: 'max', header: 'Máximo', align: 'right', render: (r) => formatUnitPrice(r.maxPrice, r.unit) },
        ]} />
    </>
  );
}
