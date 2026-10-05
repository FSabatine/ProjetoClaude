import { Button, Group, Paper, SimpleGrid, Skeleton, Stack, Table, Text } from '@mantine/core';
import dayjs from 'dayjs';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ColumnChart } from '../../components/ColumnChart';
import { HeaderFact, Section } from '../../components/DetailLayout';
import { formatCurrency, formatDate } from '../../lib/format';
import { expensesApi, financeAnalyticsApi } from './api';
import { COST_PER_KM_HELP, KpiTile, PartialTotalsNotice, PaymentStatusBadge, TCO_HELP } from './components';

const MONTH_SHORT = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

export function VehicleFinancialPanel({ vehicleId }: { vehicleId: string }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const canSeeCosts = can(PERMISSIONS.finance.viewcosts);
  const today = dayjs().format('YYYY-MM-DD');
  const monthStart = dayjs().startOf('month').format('YYYY-MM-DD');
  const yearStart = dayjs().startOf('year').format('YYYY-MM-DD');

  const monthBreakdown = financeAnalyticsApi.useVehicleBreakdown(vehicleId, monthStart, today);
  const costPerKm = financeAnalyticsApi.useVehicleCostPerKm(vehicleId, monthStart, today);
  const tco = financeAnalyticsApi.useVehicleTco(vehicleId);
  const evolution = financeAnalyticsApi.useMonthlyEvolution(dayjs(yearStart).subtract(1, 'year').format('YYYY-MM-DD'), today, vehicleId);
  const recentExpenses = expensesApi.useList({ vehicleId, pageSize: 5, sortBy: 'expenseDate', sortDirection: 'Desc' });

  if (!monthBreakdown.data || !tco.data) return <Skeleton height={320} />;
  const m = monthBreakdown.data;
  const t = tco.data;
  const isPartial = m.isPartial || t.isPartial;

  return (
    <Stack gap="md">
      {isPartial && <PartialTotalsNotice />}
      <SimpleGrid cols={{ base: 2, md: 4 }} spacing="sm">
        <KpiTile label="Custo do mês" value={formatCurrency(m.totalCost)} />
        <KpiTile label="Custo/km (mês)" help={COST_PER_KM_HELP}
          value={costPerKm.data?.hasSufficientData ? formatCurrency(costPerKm.data.costPerKm) : <Text size="sm" c="dimmed">dados insuficientes</Text>} />
        <KpiTile label="TCO acumulado" help={TCO_HELP} value={formatCurrency(t.totalCost)} hint={`desde ${formatDate(t.sinceDate)}`} />
        <KpiTile label="Custo por mês (TCO)" value={formatCurrency(t.costPerMonth)} />
      </SimpleGrid>

      <HeaderGrid month={m} />

      {evolution.data && (
        <ColumnChart title="Evolução de custo do veículo" description="Últimos 12 meses"
          points={evolution.data.items.map((e) => ({ key: `${e.year}-${e.month}`, label: `${MONTH_SHORT[e.month - 1]}/${String(e.year).slice(2)}`, value: e.amount }))}
          format={(v) => formatCurrency(v)} />
      )}

      <Section title="Despesas recentes" action={<Button variant="default" size="xs" component={Link} to={`/financeiro/despesas?vehicleId=${vehicleId}`}>Ver todas</Button>}>
        {(recentExpenses.data?.items.length ?? 0) === 0 ? <Text size="sm" c="dimmed">Nenhuma despesa manual para este veículo.</Text> : (
          <Table.ScrollContainer minWidth={480}>
            <Table highlightOnHover fz="sm">
              <Table.Thead><Table.Tr><Table.Th>Data</Table.Th><Table.Th>Descrição</Table.Th>{canSeeCosts && <Table.Th ta="right">Valor</Table.Th>}<Table.Th>Situação</Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {recentExpenses.data!.items.map((e) => (
                  <Table.Tr key={e.id} style={{ cursor: 'pointer' }} onClick={() => navigate(`/financeiro/despesas/${e.id}/editar`)}>
                    <Table.Td>{formatDate(e.expenseDate)}</Table.Td>
                    <Table.Td>{e.description}</Table.Td>
                    {canSeeCosts && <Table.Td ta="right">{formatCurrency(e.amount)}</Table.Td>}
                    <Table.Td><PaymentStatusBadge value={e.status} /></Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Section>
    </Stack>
  );
}

function HeaderGrid({ month }: { month: { fuelCost: number; maintenanceCost: number; tireCost: number; otherCost: number } }) {
  return (
    <Paper p={{ base: 'md', sm: 'lg' }}>
      <Group grow>
        <HeaderFact label="Combustível" value={formatCurrency(month.fuelCost)} />
        <HeaderFact label="Manutenção" value={formatCurrency(month.maintenanceCost)} />
        <HeaderFact label="Pneus" value={formatCurrency(month.tireCost)} />
        <HeaderFact label="Outras despesas" value={formatCurrency(month.otherCost)} />
      </Group>
    </Paper>
  );
}
