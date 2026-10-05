import { Anchor, Button, Group, Paper, SimpleGrid, Skeleton, Stack, Table, Text } from '@mantine/core';
import { IconSparkles } from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import { ColumnChart } from '../../components/ColumnChart';
import { PageHeader } from '../../components/PageHeader';
import { ErrorState } from '../../components/States';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { openAssistant } from '../assistant/assistant';
import { formatCurrency } from '../../lib/format';
import { financeAnalyticsApi } from './api';
import { COST_PER_KM_HELP, KpiTile, PartialTotalsNotice } from './components';

const MONTH_SHORT = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

export function FinancialDashboardPage() {
  const query = financeAnalyticsApi.useDashboard();
  const { can } = useAuth();
  const d = query.data;

  if (query.error && !d) return <><PageHeader title="Financeiro" /><Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper></>;

  return (
    <>
      <PageHeader title="Financeiro" description="Custos da frota: combustível, manutenção, pneus e demais despesas, num só lugar."
        action={can(PERMISSIONS.assistant.use) && (
          <Button variant="default" leftSection={<IconSparkles size={18} />} onClick={() => openAssistant('Explique os custos deste mês')}>Explicar os custos do mês</Button>
        )} />
      <Stack gap="xl">
        {!d ? (
          <SimpleGrid cols={{ base: 2, md: 4 }}>{Array.from({ length: 8 }, (_, i) => <Skeleton key={i} height={96} radius="md" />)}</SimpleGrid>
        ) : (
          <>
            {d.isPartial && <PartialTotalsNotice />}
            <SimpleGrid cols={{ base: 2, md: 4 }} spacing="sm">
              <KpiTile label="Custo da frota (ano)" value={formatCurrency(d.totalFleetCostYtd)} />
              <KpiTile label="Custo do mês" value={formatCurrency(d.monthlyCost)} />
              <KpiTile label="Custo/km da frota" help={COST_PER_KM_HELP}
                value={d.costPerKmHasSufficientData ? formatCurrency(d.fleetCostPerKm) : <Text size="sm" c="dimmed">dados insuficientes</Text>} />
              <KpiTile label="Despesas em atraso" value={d.overdueExpenseCount}
                hint={d.overdueExpenseAmount !== null ? formatCurrency(d.overdueExpenseAmount) : undefined} />
              <KpiTile label="Combustível" value={formatCurrency(d.fuelCost)} />
              <KpiTile label="Manutenção" value={formatCurrency(d.maintenanceCost)} />
              <KpiTile label="Pneus" value={formatCurrency(d.tireCost)} />
              <KpiTile label="Outras despesas" value={formatCurrency(d.otherCost)} hint={d.dueSoonRecurringCount > 0 ? `${d.dueSoonRecurringCount} recorrente(s) a vencer` : undefined} />
            </SimpleGrid>

            <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
              <ColumnChart title="Evolução mensal do custo" description="Últimos 12 meses"
                points={d.monthlyEvolution.map((m) => ({ key: `${m.year}-${m.month}`, label: `${MONTH_SHORT[m.month - 1]}/${String(m.year).slice(2)}`, value: m.amount }))}
                format={(v) => formatCurrency(v)} />
              <Paper p={{ base: 'md', sm: 'lg' }}>
                <Stack gap="sm">
                  <Text fw={650}>Custo por categoria</Text>
                  {d.costByCategory.length === 0 ? <Text size="sm" c="dimmed">Sem despesas no período.</Text> : (
                    <Table.ScrollContainer minWidth={280}>
                      <Table striped withRowBorders={false} fz="sm">
                        <Table.Tbody>
                          {d.costByCategory.map((c) => (
                            <Table.Tr key={c.categoryName}>
                              <Table.Td>{c.categoryName}</Table.Td>
                              <Table.Td ta="right" fw={500}>{formatCurrency(c.amount)}</Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </Table.ScrollContainer>
                  )}
                </Stack>
              </Paper>
            </SimpleGrid>

            <Group gap="md">
              <Anchor component={Link} to="/financeiro/ranking">Ver ranking de veículos</Anchor>
              <Anchor component={Link} to="/financeiro/relatorios">Ver relatórios</Anchor>
              <Anchor component={Link} to="/financeiro/despesas?status=Overdue">Ver despesas em atraso</Anchor>
            </Group>
          </>
        )}
      </Stack>
    </>
  );
}
