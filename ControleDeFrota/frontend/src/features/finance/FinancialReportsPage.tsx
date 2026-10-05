import { useState } from 'react';
import { Anchor, Paper, SimpleGrid, Skeleton, Stack, Table, Tabs, Text } from '@mantine/core';
import dayjs from 'dayjs';
import { Link, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ColumnChart } from '../../components/ColumnChart';
import { VehiclePicker } from '../../components/EntityPickers';
import { PageHeader } from '../../components/PageHeader';
import { formatCurrency, formatDate } from '../../lib/format';
import { usePeriod, PeriodFilter } from '../fuel/components';
import { budgetsApi, financeAnalyticsApi } from './api';
import { BudgetProgress, BudgetStatusBadge, KpiTile, PartialTotalsNotice, TCO_HELP } from './components';

const MONTH_SHORT = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

export function FinancialReportsPage() {
  const { can } = useAuth();
  const canSeeCosts = can(PERMISSIONS.finance.viewcosts);
  const [params, setParams] = useSearchParams();
  const period = usePeriod(30);
  const tabs = ['despesas', ...(canSeeCosts ? ['centro-de-custo', 'mensal', 'orcado', 'tco'] : [])];
  const tab = tabs.includes(params.get('relatorio') ?? '') ? params.get('relatorio')! : 'despesas';

  return (
    <>
      <PageHeader title="Relatórios financeiros" description="Valores operacionais do período (não são valores contábeis)."
        breadcrumbs={[{ label: 'Financeiro', to: '/financeiro' }, { label: 'Relatórios' }]} />
      <Tabs value={tab} keepMounted={false}
        onChange={(v) => setParams((p) => { const n = new URLSearchParams(p); n.set('relatorio', v ?? 'despesas'); return n; }, { replace: true })}>
        <Tabs.List mb="md">
          <Tabs.Tab value="despesas">Despesas</Tabs.Tab>
          {canSeeCosts && <Tabs.Tab value="centro-de-custo">Custo por centro de custo</Tabs.Tab>}
          {canSeeCosts && <Tabs.Tab value="mensal">Custo mensal da frota</Tabs.Tab>}
          {canSeeCosts && <Tabs.Tab value="orcado">Orçado x Realizado</Tabs.Tab>}
          {canSeeCosts && <Tabs.Tab value="tco">TCO</Tabs.Tab>}
        </Tabs.List>
        <Tabs.Panel value="despesas">
          <Paper p="md">
            <Text size="sm">Para o relatório completo de despesas, com todos os filtros, use a <Anchor component={Link} to="/financeiro/despesas">lista de despesas</Anchor>.</Text>
          </Paper>
        </Tabs.Panel>
        {canSeeCosts && <Tabs.Panel value="centro-de-custo"><CostCenterReport period={period} /></Tabs.Panel>}
        {canSeeCosts && <Tabs.Panel value="mensal"><MonthlyFleetReport period={period} /></Tabs.Panel>}
        {canSeeCosts && <Tabs.Panel value="orcado"><BudgetVsActualReport /></Tabs.Panel>}
        {canSeeCosts && <Tabs.Panel value="tco"><TcoReport /></Tabs.Panel>}
      </Tabs>
    </>
  );
}

function CostCenterReport({ period }: { period: ReturnType<typeof usePeriod> }) {
  const query = financeAnalyticsApi.useCostByCostCenter(period.from, period.to);
  return (
    <Stack gap="md">
      <PeriodFilter period={period} />
      {query.data?.isPartial && <PartialTotalsNotice />}
      <Paper>
        {!query.data ? <Skeleton height={200} m="md" /> : query.data.items.length === 0 ? <Text size="sm" c="dimmed" p="md">Sem despesas manuais no período.</Text> : (
          <Table.ScrollContainer minWidth={360}>
            <Table highlightOnHover>
              <Table.Thead><Table.Tr><Table.Th>Centro de custo</Table.Th><Table.Th ta="right">Custo</Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {query.data.items.map((c) => (
                  <Table.Tr key={c.costCenterId ?? 'none'}><Table.Td>{c.costCenterName}</Table.Td><Table.Td ta="right">{formatCurrency(c.amount)}</Table.Td></Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Paper>
    </Stack>
  );
}

function MonthlyFleetReport({ period }: { period: ReturnType<typeof usePeriod> }) {
  const from = dayjs(period.to).subtract(11, 'month').startOf('month').format('YYYY-MM-DD');
  const query = financeAnalyticsApi.useMonthlyEvolution(from, period.to);
  return (
    <Stack gap="md">
      {query.data?.isPartial && <PartialTotalsNotice />}
      {!query.data ? <Skeleton height={240} /> : (
        <ColumnChart title="Custo mensal da frota" description="Últimos 12 meses"
          points={query.data.items.map((m) => ({ key: `${m.year}-${m.month}`, label: `${MONTH_SHORT[m.month - 1]}/${String(m.year).slice(2)}`, value: m.amount }))}
          format={(v) => formatCurrency(v)} />
      )}
    </Stack>
  );
}

function BudgetVsActualReport() {
  const year = new Date().getFullYear();
  const query = budgetsApi.useVsActual(year, undefined);
  const items = query.data ?? [];
  return (
    <Stack gap="md">
      {items.some((i) => i.isPartial) && <PartialTotalsNotice />}
      {items.length === 0 ? <Paper p="md"><Text size="sm" c="dimmed">Nenhum orçamento cadastrado para {year}.</Text></Paper> : (
        <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
          {items.map((v) => (
            <Paper key={v.budget.id} p="md">
              <Text fw={600} mb="xs">{v.budget.categoryName}</Text>
              <BudgetProgress utilizationPercent={v.utilizationPercent} status={v.status} />
              <Text size="sm" c="dimmed" mt="xs">Orçado {formatCurrency(v.budget.amount)} · Realizado {formatCurrency(v.actual)}</Text>
              <BudgetStatusBadge value={v.status} />
            </Paper>
          ))}
        </SimpleGrid>
      )}
    </Stack>
  );
}

function TcoReport() {
  const [vehicleId, setVehicleId] = useState<string | null>(null);
  const query = financeAnalyticsApi.useVehicleTco(vehicleId);
  return (
    <Stack gap="md">
      <VehiclePicker label="Veículo" value={vehicleId} onChange={setVehicleId} maw={360} />
      {vehicleId && !query.data && <Skeleton height={200} />}
      {query.data && (
        <>
          {query.data.isPartial && <PartialTotalsNotice />}
          <SimpleGrid cols={{ base: 2, md: 4 }} spacing="sm">
            <KpiTile label="Valor de aquisição" value={formatCurrency(query.data.acquisitionValue)} />
            <KpiTile label="Custo operacional acumulado" value={formatCurrency(query.data.operatingCost)} />
            <KpiTile label="TCO total" help={TCO_HELP} value={formatCurrency(query.data.totalCost)} hint={`desde ${formatDate(query.data.sinceDate)}`} />
            <KpiTile label="Custo/km (TCO)" value={query.data.hasSufficientMileageData ? formatCurrency(query.data.costPerKm) : <Text size="sm" c="dimmed">dados insuficientes</Text>} />
          </SimpleGrid>
        </>
      )}
    </Stack>
  );
}
