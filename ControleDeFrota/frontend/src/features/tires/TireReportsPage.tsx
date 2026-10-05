import { useState } from 'react';
import { Anchor, Badge, Group, Paper, Select, Tabs, Text, TextInput } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import dayjs from 'dayjs';
import { Link, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge, toSelectData } from '../../components/common';
import { VehiclePicker } from '../../components/EntityPickers';
import { PageHeader } from '../../components/PageHeader';
import { pagedLoader, ReportTable, useReportState } from '../../components/ReportTable';
import { formatCurrency, formatDate, formatDateTime, formatNumber } from '../../lib/format';
import { formatCostPerKm, formatPressure, formatTread } from '../../lib/tires';
import { InfoHint } from '../fuel/components';
import { tireAnalyticsApi, tireModelsApi } from './api';
import { COST_PER_KM_HELP } from './TireDetailPage';
import { locationText } from './TireListPage';
import { PRESSURE_CHECK, REMOVAL_REASON, TIRE_CONDITION, TIRE_STATUS, WEAR_PATTERN } from './tires';

const code = (id: string, value: string) => <Anchor component={Link} to={`/pneus/${id}`} ff="monospace" fw={700} size="sm">{value}</Anchor>;

/** /pneus/relatorios — inventory, lifecycle, inspections and costs (seção 45), filtered and paged on the server. */
export function TireReportsPage() {
  const { can } = useAuth();
  const canSeeCosts = can(PERMISSIONS.tires.viewcosts);
  const [params, setParams] = useSearchParams();
  const tabs = ['inventario', 'ciclo-de-vida', 'inspecoes', ...(canSeeCosts ? ['custos'] : [])];
  const tab = tabs.includes(params.get('relatorio') ?? '') ? params.get('relatorio')! : 'inventario';
  const [range, setRange] = useState<[Date | null, Date | null]>([dayjs().subtract(89, 'day').toDate(), new Date()]);
  const [vehicleId, setVehicleId] = useState<string | null>(null);
  const [brand, setBrand] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [condition, setCondition] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const brands = tireModelsApi.useBrands();
  const period = { from: range[0] ? dayjs(range[0]).format('YYYY-MM-DD') : undefined, to: range[1] ? dayjs(range[1]).format('YYYY-MM-DD') : undefined };
  const filters = { vehicleId, brand, status, search: search || undefined };

  const inventoryState = useReportState('code');
  const lifecycleState = useReportState('installedAt', 'Desc');
  const inspectionState = useReportState('inspectedAt', 'Desc');
  const costState = useReportState('code');
  const inventory = tireAnalyticsApi.useInventory({ ...filters, ...inventoryState.params }, tab === 'inventario');
  const lifecycle = tireAnalyticsApi.useLifecycle({ ...filters, ...period, ...lifecycleState.params }, tab === 'ciclo-de-vida');
  const inspections = tireAnalyticsApi.useInspections({ ...filters, ...period, condition, ...inspectionState.params }, tab === 'inspecoes');
  const costs = tireAnalyticsApi.useCosts({ ...filters, ...period, ...costState.params }, tab === 'custos' && canSeeCosts);
  const usesPeriod = tab !== 'inventario';

  return (
    <>
      <PageHeader title="Relatórios de pneus" description="Inventário, ciclo de vida, inspeções e custos."
        breadcrumbs={[{ label: 'Painel de pneus', to: '/pneus/painel' }, { label: 'Relatórios' }]} />
      <Paper p="sm" mb="md">
        <Group gap="sm" wrap="wrap">
          {usesPeriod && (
            <DatePickerInput type="range" aria-label="Período" valueFormat="DD/MM/YYYY" maxDate={new Date()} value={range} onChange={setRange}
              description={tab === 'custos' ? 'Pneus comprados no período' : tab === 'ciclo-de-vida' ? 'Instalações no período' : 'Inspeções no período'}
              style={{ flex: '1 1 240px', maxWidth: 300 }} />
          )}
          <VehiclePicker aria-label="Veículo" placeholder="Veículo" value={vehicleId} onChange={setVehicleId} style={{ flex: '1 1 200px' }} />
          <Select aria-label="Marca" placeholder="Marca" clearable data={(brands.data ?? []).map((b) => ({ value: b, label: b }))} value={brand} onChange={setBrand} style={{ flex: '0 1 180px' }} />
          {tab !== 'inspecoes' && <Select aria-label="Situação" placeholder="Situação" clearable data={toSelectData(TIRE_STATUS)} value={status} onChange={setStatus} style={{ flex: '0 1 180px' }} />}
          {tab === 'inspecoes' && <Select aria-label="Condição" placeholder="Condição" clearable data={toSelectData(TIRE_CONDITION)} value={condition} onChange={setCondition} style={{ flex: '0 1 180px' }} />}
          <TextInput aria-label="Número de fogo" placeholder="Nº de fogo" value={search} onChange={(e) => setSearch(e.currentTarget.value)} style={{ flex: '0 1 160px' }} />
        </Group>
      </Paper>
      <Tabs value={tab} onChange={(v) => setParams((p) => { const n = new URLSearchParams(p); n.set('relatorio', v ?? 'inventario'); return n; }, { replace: true })}>
        <Tabs.List mb="md">
          <Tabs.Tab value="inventario">Inventário</Tabs.Tab>
          <Tabs.Tab value="ciclo-de-vida">Ciclo de vida</Tabs.Tab>
          <Tabs.Tab value="inspecoes">Inspeções</Tabs.Tab>
          {canSeeCosts && <Tabs.Tab value="custos">Custos</Tabs.Tab>}
        </Tabs.List>
        <Paper>
          <Tabs.Panel value="inventario">
            <ReportTable exportAs={{ title: 'Inventário de pneus', load: pagedLoader('/tires/reports/inventory', { ...filters, ...inventoryState.params }, (d) => (d as { rows: { items: never[]; totalPages: number } }).rows) }} data={inventory.data?.rows} isLoading={inventory.isFetching} error={inventory.error} onRetry={() => void inventory.refetch()}
              getKey={(r) => r.id} state={inventoryState} empty="Nenhum pneu com estes filtros."
              columns={[
                { key: 'code', header: 'Nº de fogo', sortKey: 'code', render: (r) => code(r.id, r.code) },
                { key: 'brand', header: 'Marca', sortKey: 'brand', render: (r) => r.brand },
                { key: 'model', header: 'Modelo', render: (r) => r.modelName },
                { key: 'size', header: 'Medida', sortKey: 'size', render: (r) => r.size },
                { key: 'status', header: 'Situação', sortKey: 'status', render: (r) => <StatusBadge value={r.status} map={TIRE_STATUS} /> },
                { key: 'location', header: 'Local / veículo / posição', render: (r) => locationText(r) },
                { key: 'tread', header: 'Sulco', sortKey: 'treadDepth', align: 'right', render: (r) => formatTread(r.currentTreadDepthMm) },
                { key: 'km', header: 'Km', sortKey: 'km', align: 'right', render: (r) => formatNumber(r.currentKm) },
              ]} />
          </Tabs.Panel>
          <Tabs.Panel value="ciclo-de-vida">
            <ReportTable exportAs={{ title: 'Ciclo de vida dos pneus', load: pagedLoader('/tires/reports/lifecycle', { ...filters, ...period, ...lifecycleState.params }, (d) => (d as { rows: { items: never[]; totalPages: number } }).rows) }} data={lifecycle.data?.rows} isLoading={lifecycle.isFetching} error={lifecycle.error} onRetry={() => void lifecycle.refetch()}
              getKey={(r) => r.installationId} state={lifecycleState} empty="Nenhuma instalação no período."
              columns={[
                { key: 'code', header: 'Pneu', sortKey: 'code', render: (r) => <>{code(r.tireId, r.code)}<Text size="xs" c="dimmed">{r.brand} {r.modelName}</Text></> },
                { key: 'where', header: 'Veículo / posição', render: (r) => <>{r.assetLabel}<Text size="xs" c="dimmed">{r.positionLabel}</Text></> },
                { key: 'installed', header: 'Instalação', sortKey: 'installedAt', render: (r) => formatDate(r.installedAt) },
                { key: 'removed', header: 'Remoção', render: (r) => r.removedAt ? <>{formatDate(r.removedAt)}<Text size="xs" c="dimmed">{r.removalReason ? REMOVAL_REASON[r.removalReason].label : ''}</Text></> : <Badge variant="light">Atual</Badge> },
                { key: 'distance', header: 'Km na posição', sortKey: 'distance', align: 'right', render: (r) => formatNumber(r.distanceKm) },
                { key: 'tireKm', header: 'Km do pneu', align: 'right', render: (r) => formatNumber(r.tireKm) },
                { key: 'repairs', header: 'Consertos', align: 'right', render: (r) => r.repairCount },
                { key: 'retreads', header: 'Recap.', align: 'right', render: (r) => r.retreadCount },
                ...(lifecycle.data?.canSeeCosts ? [
                  { key: 'cost', header: 'Custo do ciclo', align: 'right' as const, render: (r: { lifecycleCost: number | null }) => formatCurrency(r.lifecycleCost) },
                  { key: 'perKm', header: 'Custo/km', align: 'right' as const, render: (r: { costPerKm: number | null }) => formatCostPerKm(r.costPerKm) },
                ] : []),
              ]} />
          </Tabs.Panel>
          <Tabs.Panel value="inspecoes">
            <ReportTable exportAs={{ title: 'Inspeções de pneus', load: pagedLoader('/tires/reports/inspections', { ...filters, ...period, condition, ...inspectionState.params }, (d) => (d as { rows: { items: never[]; totalPages: number } }).rows) }} data={inspections.data?.rows} isLoading={inspections.isFetching} error={inspections.error} onRetry={() => void inspections.refetch()}
              getKey={(r) => r.inspectionId} state={inspectionState} empty="Nenhuma inspeção no período."
              columns={[
                { key: 'code', header: 'Pneu', sortKey: 'code', render: (r) => code(r.tireId, r.code) },
                { key: 'where', header: 'Veículo / posição', render: (r) => r.assetLabel ? <>{r.assetLabel}<Text size="xs" c="dimmed">{r.positionLabel}</Text></> : <Text size="xs" c="dimmed">fora do veículo</Text> },
                { key: 'tread', header: 'Sulco', sortKey: 'tread', align: 'right', render: (r) => formatTread(r.treadDepthMm) },
                { key: 'pressure', header: 'Pressão', align: 'right', render: (r) => <>{formatPressure(r.pressure, r.pressureUnit)}{r.pressureCheck && r.pressureCheck !== 'WithinRange' && <Text size="xs" c="dimmed">{PRESSURE_CHECK[r.pressureCheck].label}</Text>}</> },
                { key: 'condition', header: 'Condição', render: (r) => <StatusBadge value={r.condition} map={TIRE_CONDITION} /> },
                { key: 'wear', header: 'Desgaste', render: (r) => <>{WEAR_PATTERN[r.wearPattern].label}{r.damageCount > 0 && <Text size="xs" c="red">{r.damageCount} dano(s)</Text>}</> },
                { key: 'date', header: 'Data', sortKey: 'inspectedAt', render: (r) => formatDateTime(r.inspectedAt) },
              ]} />
          </Tabs.Panel>
          {canSeeCosts && (
            <Tabs.Panel value="custos">
              <Group gap={4} p="sm" pb={0}><Text size="sm" c="dimmed">Custo do ciclo de vida de cada pneu (desde a compra).</Text><InfoHint label="Custo por km">{COST_PER_KM_HELP}</InfoHint></Group>
              <ReportTable exportAs={{ title: 'Custos de pneus', load: pagedLoader('/tires/reports/costs', { ...filters, ...period, ...costState.params }, (d) => (d as { rows: { items: never[]; totalPages: number } }).rows) }} data={costs.data?.rows} isLoading={costs.isFetching} error={costs.error} onRetry={() => void costs.refetch()}
                getKey={(r) => r.tireId} state={costState} empty="Nenhum pneu comprado no período."
                columns={[
                  { key: 'code', header: 'Pneu', sortKey: 'code', render: (r) => <>{code(r.tireId, r.code)}<Text size="xs" c="dimmed">{r.brand} {r.modelName}</Text></> },
                  { key: 'purchase', header: 'Compra', sortKey: 'purchase', align: 'right', render: (r) => formatCurrency(r.purchase) },
                  { key: 'repairs', header: 'Consertos', align: 'right', render: (r) => formatCurrency(r.repairs) },
                  { key: 'retreads', header: 'Recapagens', align: 'right', render: (r) => formatCurrency(r.retreads) },
                  { key: 'other', header: 'Outros', align: 'right', render: (r) => formatCurrency(r.other) },
                  { key: 'total', header: 'Total', align: 'right', render: (r) => <Text size="sm" fw={600}>{formatCurrency(r.total)}</Text> },
                  { key: 'km', header: 'Km', sortKey: 'km', align: 'right', render: (r) => <>{formatNumber(r.km)}{r.hasUnmeasuredDistance && <Text size="xs" c="dimmed">incompleto</Text>}</> },
                  { key: 'perKm', header: 'Custo/km', align: 'right', render: (r) => formatCostPerKm(r.costPerKm) },
                ]} />
            </Tabs.Panel>
          )}
        </Paper>
      </Tabs>
    </>
  );
}
