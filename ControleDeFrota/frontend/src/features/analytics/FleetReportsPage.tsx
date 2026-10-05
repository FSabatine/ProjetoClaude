import { useState } from 'react';
import { Alert, Anchor, Button, Group, Paper, Select, SimpleGrid, Skeleton, Table, Tabs, Text } from '@mantine/core';
import { IconArrowsDiff, IconChartBar } from '@tabler/icons-react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { toSelectData } from '../../components/common';
import { ExportMenu } from '../../components/ExportMenu';
import { PageHeader } from '../../components/PageHeader';
import { pagedLoader, ReportTable, useReportState, type ReportColumn } from '../../components/ReportTable';
import { formatCurrency, formatDate, formatNumber, formatPlate } from '../../lib/format';
import { InfoHint } from '../finance/components';
import { PeriodFilter, usePeriod } from '../fuel/components';
import { VEHICLE_TYPE } from '../vehicles/vehicles';
import { analyticsApi, type VehicleMetrics } from './analytics';

const plateLink = (r: VehicleMetrics, tab?: string) => (
  <Anchor component={Link} to={`/veiculos/${r.vehicleId}${tab ? `?aba=${tab}` : ''}`} ff="monospace" size="sm">{formatPlate(r.licensePlate)}</Anchor>
);

const km = (r: VehicleMetrics) => (r.kmDriven === null ? '—' : `${formatNumber(r.kmDriven)} km${r.kmReliable ? '' : '*'}`);

export function FleetReportsPage() {
  const [params, setParams] = useSearchParams();
  const { can } = useAuth();
  const navigate = useNavigate();
  const period = usePeriod(90);
  const tabs = ['desempenho', ...(can(PERMISSIONS.maintenance.view) ? ['manutencao'] : [])];
  const tab = tabs.includes(params.get('relatorio') ?? '') ? params.get('relatorio')! : 'desempenho';

  return (
    <>
      <PageHeader title="Relatórios da frota"
        description="Visão cruzada de quilometragem, combustível, manutenção, pneus e custos por veículo. Os relatórios de cada módulo continuam nas telas do módulo."
        action={<Button variant="default" leftSection={<IconArrowsDiff size={18} />} onClick={() => navigate('/veiculos/comparar')}>Comparar veículos</Button>} />
      <PeriodFilter period={period} />
      <Tabs value={tab} keepMounted={false}
        onChange={(v) => setParams((p) => { const n = new URLSearchParams(p); n.set('relatorio', v ?? 'desempenho'); return n; }, { replace: true })}>
        <Tabs.List mb="md">
          <Tabs.Tab value="desempenho">Desempenho da frota</Tabs.Tab>
          {tabs.includes('manutencao') && <Tabs.Tab value="manutencao">Manutenção</Tabs.Tab>}
        </Tabs.List>
        <Tabs.Panel value="desempenho"><PerformanceReport from={period.from} to={period.to} /></Tabs.Panel>
        {tabs.includes('manutencao') && <Tabs.Panel value="manutencao"><MaintenanceReport from={period.from} to={period.to} /></Tabs.Panel>}
      </Tabs>
      <Paper p="md" mt="lg">
        <Group gap="xs" mb={6}><IconChartBar size={18} /><Text fw={600}>Outros relatórios</Text></Group>
        <Group gap="lg">
          {can(PERMISSIONS.fuel.view) && <Anchor component={Link} to="/combustivel/relatorios">Combustível</Anchor>}
          {can(PERMISSIONS.tires.view) && <Anchor component={Link} to="/pneus/relatorios">Pneus</Anchor>}
          {can(PERMISSIONS.finance.view) && <Anchor component={Link} to="/financeiro/relatorios">Financeiro</Anchor>}
          {can(PERMISSIONS.finance.viewcosts) && <Anchor component={Link} to="/financeiro/ranking">Ranking de custo por veículo</Anchor>}
        </Group>
      </Paper>
    </>
  );
}

function useTypeFilter() {
  const [type, setType] = useState<string | null>(null);
  const select = (
    <Select placeholder="Tipo de veículo" aria-label="Tipo de veículo" data={toSelectData(VEHICLE_TYPE)} value={type} onChange={setType} clearable w={220} />
  );
  return { type, select };
}

function PerformanceReport({ from, to }: { from: string; to: string }) {
  const state = useReportState('totalCost', 'Desc');
  const { type, select } = useTypeFilter();
  const query = analyticsApi.useFleetPerformance({ from, to, type, ...state.params });
  const partial = query.data?.items.some((r) => r.isPartial);
  const columns: ReportColumn<VehicleMetrics>[] = [
    { key: 'vehicle', header: 'Veículo', render: (r) => plateLink(r), exportValue: (r) => formatPlate(r.licensePlate) },
    { key: 'model', header: 'Modelo', render: (r) => r.model, exportValue: (r) => r.model },
    { key: 'km', header: 'Km rodados', sortKey: 'kmDriven', align: 'right', render: km, exportValue: (r) => r.kmDriven },
    { key: 'consumption', header: 'Consumo (km/l)', sortKey: 'averageConsumption', align: 'right',
      render: (r) => (r.averageConsumption === null ? '—' : r.averageConsumption.toLocaleString('pt-BR')), exportValue: (r) => r.averageConsumption },
    { key: 'fuel', header: 'Combustível', sortKey: 'fuelCost', align: 'right', render: (r) => formatCurrency(r.fuelCost), exportValue: (r) => r.fuelCost },
    { key: 'maintenance', header: 'Manutenção', sortKey: 'maintenanceCost', align: 'right', render: (r) => formatCurrency(r.maintenanceCost), exportValue: (r) => r.maintenanceCost },
    { key: 'tires', header: 'Pneus', sortKey: 'tireCost', align: 'right', render: (r) => formatCurrency(r.tireCost), exportValue: (r) => r.tireCost },
    { key: 'other', header: 'Outras', align: 'right', render: (r) => formatCurrency(r.otherCost), exportValue: (r) => r.otherCost },
    { key: 'total', header: 'Custo total', sortKey: 'totalCost', align: 'right', render: (r) => formatCurrency(r.totalCost), exportValue: (r) => r.totalCost },
    { key: 'costPerKm', header: 'Custo/km', sortKey: 'costPerKm', align: 'right', render: (r) => formatCurrency(r.costPerKm), exportValue: (r) => r.costPerKm },
  ];
  return (
    <Paper>
      <Group p="sm" pb={0} gap="sm" justify="space-between">
        {select}
        <Group gap={4}>
          <Text size="xs" c="dimmed">* km sem leitura antes do período: custo/km não calculado</Text>
          <InfoHint label="Como os números são calculados">
            Km = leitura de hodômetro mais alta até o fim do período menos a mais alta antes do início. Consumo = km ÷ litros dos trechos de tanque cheio.
            Custos vêm direto de cada módulo (sem lançar de novo). Valores aparecem só para quem tem permissão; "—" significa sem acesso ou sem dado.
          </InfoHint>
        </Group>
      </Group>
      {partial && <Alert color="orange" variant="light" m="sm" mb={0}>Totais parciais: você não tem permissão de custo para todas as fontes, então o total e o custo/km não incluem tudo.</Alert>}
      <ReportTable state={state} data={query.data} isLoading={query.isFetching} error={query.error} onRetry={() => void query.refetch()}
        getKey={(r) => r.vehicleId} empty="Nenhum veículo com os filtros escolhidos." columns={columns}
        exportAs={{ title: 'Desempenho da frota', subtitle: `Período ${formatDate(from)} a ${formatDate(to)}`, load: pagedLoader('/analytics/fleet-performance', { from, to, type, ...state.params }) }} />
    </Paper>
  );
}

function MaintenanceReport({ from, to }: { from: string; to: string }) {
  const state = useReportState('maintenanceCost', 'Desc');
  const { type, select } = useTypeFilter();
  const query = analyticsApi.useFleetPerformance({ from, to, type, ...state.params });
  const recurring = analyticsApi.useRecurringProblems(from, to);
  const columns: ReportColumn<VehicleMetrics>[] = [
    { key: 'vehicle', header: 'Veículo', render: (r) => plateLink(r, 'manutencao'), exportValue: (r) => formatPlate(r.licensePlate) },
    { key: 'orders', header: 'OS concluídas', sortKey: 'workOrdersCompleted', align: 'right', render: (r) => r.workOrdersCompleted ?? '—', exportValue: (r) => r.workOrdersCompleted },
    { key: 'corrective', header: 'Corretivas', align: 'right', render: (r) => r.correctiveWorkOrders ?? '—', exportValue: (r) => r.correctiveWorkOrders },
    { key: 'downtime', header: 'Tempo parado', sortKey: 'downtimeHours', align: 'right',
      render: (r) => (r.downtimeHours === null ? '—' : `${r.downtimeHours.toLocaleString('pt-BR')} h`), exportValue: (r) => r.downtimeHours },
    { key: 'cost', header: 'Custo de manutenção', sortKey: 'maintenanceCost', align: 'right', render: (r) => formatCurrency(r.maintenanceCost), exportValue: (r) => r.maintenanceCost },
    { key: 'tires', header: 'Trocas de pneu', sortKey: 'tireReplacements', align: 'right', render: (r) => r.tireReplacements ?? '—', exportValue: (r) => r.tireReplacements },
  ];
  const recurringRows = recurring.data ?? [];
  return (
    <SimpleGrid cols={{ base: 1 }} spacing="lg">
      <Paper>
        <Group p="sm" pb={0}>{select}</Group>
        <ReportTable state={state} data={query.data} isLoading={query.isFetching} error={query.error} onRetry={() => void query.refetch()}
          getKey={(r) => r.vehicleId} empty="Nenhum veículo com os filtros escolhidos." columns={columns}
          exportAs={{ title: 'Manutenção por veículo', subtitle: `Período ${formatDate(from)} a ${formatDate(to)}`, load: pagedLoader('/analytics/fleet-performance', { from, to, type, ...state.params }) }} />
      </Paper>
      <Paper p="md">
        <Group justify="space-between" mb="sm">
          <div>
            <Group gap={4}>
              <Text fw={650}>Problemas recorrentes</Text>
              <InfoHint label="Problemas recorrentes">O mesmo serviço corretivo feito duas ou mais vezes no mesmo veículo dentro do período (a descrição é comparada sem diferenciar maiúsculas, acentos e pontuação).</InfoHint>
            </Group>
            <Text size="sm" c="dimmed">Pode indicar reparo que não resolveu a causa, peça de baixa qualidade ou uso inadequado — requer análise.</Text>
          </div>
          {recurringRows.length > 0 && (
            <ExportMenu source={{
              title: 'Problemas recorrentes', subtitle: `Período ${formatDate(from)} a ${formatDate(to)}`,
              columns: [
                { header: 'Veículo', value: (r) => formatPlate(r.licensePlate) },
                { header: 'Problema', value: (r) => r.problem },
                { header: 'Vezes', value: (r) => r.occurrences },
                { header: 'Última conclusão', value: (r) => formatDate(r.lastCompletedAt) },
                { header: 'Ordens de serviço', value: (r) => r.workOrderNumbers.map((n) => `OS-${String(n).padStart(6, '0')}`).join(', ') },
              ],
              load: async () => ({ rows: recurringRows, truncated: false }),
            }} />
          )}
        </Group>
        {!recurring.data ? <Skeleton height={80} /> : recurringRows.length === 0 ? (
          <Text size="sm" c="dimmed">Nenhum problema repetido no período.</Text>
        ) : (
          <Table.ScrollContainer minWidth={520}>
            <Table striped fz="sm">
              <Table.Thead><Table.Tr><Table.Th>Veículo</Table.Th><Table.Th>Problema</Table.Th><Table.Th ta="right">Vezes</Table.Th><Table.Th>Última conclusão</Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {recurringRows.map((r) => (
                  <Table.Tr key={`${r.vehicleId}-${r.problem}`}>
                    <Table.Td><Anchor component={Link} to={`/veiculos/${r.vehicleId}?aba=manutencao`} ff="monospace" size="sm">{formatPlate(r.licensePlate)}</Anchor></Table.Td>
                    <Table.Td>{r.problem}</Table.Td>
                    <Table.Td ta="right">{r.occurrences}</Table.Td>
                    <Table.Td>{formatDate(r.lastCompletedAt)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Paper>
    </SimpleGrid>
  );
}
