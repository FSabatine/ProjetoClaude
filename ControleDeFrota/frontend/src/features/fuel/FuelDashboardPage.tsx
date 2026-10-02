import { Alert, Anchor, Badge, Button, Group, Paper, SimpleGrid, Skeleton, Stack, Table, Text } from '@mantine/core';
import { IconAlertTriangle, IconChartBar, IconCircleCheck, IconPlus } from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ColumnChart } from '../../components/ColumnChart';
import { StatusBadge } from '../../components/common';
import { Section } from '../../components/DetailLayout';
import { PageHeader } from '../../components/PageHeader';
import { ErrorState } from '../../components/States';
import { formatCurrency, formatDate, formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { formatConsumption, formatDeviation, formatQuantity } from '../../lib/fuel';
import { fuelAnalyticsApi } from './api';
import { COST_PER_KM_HELP, CONSUMPTION_HELP, KpiTile, PeriodFilter, usePeriod } from './components';
import { FUEL_ANOMALY_TYPE, FUELING_STATUS, type VehicleFuelMetric } from './fuel';

export function FuelDashboardPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const period = usePeriod(30);
  const query = fuelAnalyticsApi.useDashboard({ from: period.from, to: period.to });
  const d = query.data;
  const periodText = `${formatDate(period.from)} a ${formatDate(period.to)}`;

  return (
    <>
      <PageHeader title="Combustível" description="Consumo, custos e alertas de abastecimento da frota."
        action={can(PERMISSIONS.fuel.create) && <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/abastecimentos/novo')}>Registrar abastecimento</Button>} />
      <PeriodFilter period={period} />
      {query.error && !d && <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper>}
      {!d && !query.error && <Stack><Skeleton height={90} /><Skeleton height={260} /></Stack>}
      {d && (
        <Stack gap="md" style={{ opacity: query.isFetching ? 0.7 : 1 }}>
          <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }} spacing="sm">
            {d.canSeeCosts && <KpiTile label="Custo total" value={formatCurrency(d.totals.totalCost)} hint={periodText} />}
            <KpiTile label="Litros" value={formatNumber(Math.round(d.totals.liters))} hint={d.totals.otherUnitsQuantity > 0 ? `+ ${formatNumber(d.totals.otherUnitsQuantity)} em m³/kWh` : 'abastecidos no período'} />
            {d.canSeeCosts && <KpiTile label="Preço médio/L" value={d.totals.averagePricePerLiter === null ? '—' : `R$ ${d.totals.averagePricePerLiter.toLocaleString('pt-BR', { minimumFractionDigits: 3 })}`} hint="total pago ÷ litros" />}
            <KpiTile label="Consumo médio" value={formatConsumption(d.totals.averageConsumption)} hint="km rodados ÷ litros, trechos medidos" help={CONSUMPTION_HELP} />
            {d.canSeeCosts && <KpiTile label="Custo/km" value={formatCurrency(d.totals.costPerKm)} hint={`${formatNumber(d.totals.measuredDistanceKm)} km medidos`} help={COST_PER_KM_HELP} />}
            <KpiTile label="Abastecimentos" value={formatNumber(d.totals.fuelings)} hint={periodText} />
          </SimpleGrid>

          <Section title="O que requer atenção" description="Abastecimentos com alerta aguardando revisão (todos os períodos). Um alerta pede conferência, não indica erro."
            action={d.pendingReview > 0 && <Button variant="default" size="xs" component={Link} to="/abastecimentos?status=PendingReview">Ver todos ({d.pendingReview})</Button>}>
            {d.attention.length === 0 ? (
              <Alert color="teal" icon={<IconCircleCheck size={18} />} p="xs">Nenhum abastecimento aguardando revisão.</Alert>
            ) : (
              <Stack gap="xs">
                {d.consumptionAnomalies > 0 && (
                  <Text size="sm" c="dimmed">{d.consumptionAnomalies} alerta(s) de consumo fora do esperado. Vale conferir abastecimentos não registrados ou o hodômetro antes de pensar em manutenção.</Text>
                )}
                {d.attention.map((a) => (
                  <Alert key={a.fuelingId} color="orange" p="xs" icon={<IconAlertTriangle size={18} />}>
                    <Group justify="space-between" gap="xs" wrap="wrap">
                      <div style={{ minWidth: 0, flex: '1 1 300px' }}>
                        <Anchor component={Link} to={`/abastecimentos/${a.fuelingId}`} fw={600} size="sm">{formatPlate(a.licensePlate)} · {formatDateTime(a.fueledAt)}</Anchor>
                        <Text size="sm">{a.message}</Text>
                      </div>
                      <Group gap={4}>{a.anomalyTypes.map((t) => <Badge key={t} color="orange" variant="light">{FUEL_ANOMALY_TYPE[t].label}</Badge>)}</Group>
                    </Group>
                  </Alert>
                ))}
              </Stack>
            )}
          </Section>

          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
            {d.canSeeCosts && (
              <ColumnChart title="Gasto mensal com combustível" description="Últimos 12 meses (todos os combustíveis)"
                points={d.monthly.map((m) => ({ key: m.start, label: m.label, value: m.cost && m.cost > 0 ? m.cost : null }))}
                format={(v) => formatCurrency(v)} />
            )}
            <ColumnChart title="Consumo médio por mês (km/L)" description="Só meses com trechos medidos de tanque cheio a tanque cheio"
              points={d.monthly.map((m) => ({ key: m.start, label: m.label, value: m.consumption }))}
              format={(v) => formatConsumption(v)} emptyText="Ainda não há trechos medidos. O consumo aparece a partir do segundo tanque cheio de cada veículo." />
          </SimpleGrid>

          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
            {d.canSeeCosts && (
              <VehicleTable title="Maior custo de combustível" description={`Custo total no período (${periodText})`} rows={d.highestCostVehicles}
                metric={(v) => formatCurrency(v.cost)} metricHeader="Custo" extra={(v) => formatCurrency(v.costPerKm)} extraHeader="Custo/km" />
            )}
            <VehicleTable title="Consumo abaixo do esperado" description={`Diferença do consumo real para o esperado no período (${periodText})`}
              rows={d.lowestConsumptionVehicles.filter((v) => (v.variancePercent ?? 0) < 0 || v.expectedConsumption === null)}
              metric={(v) => formatConsumption(v.consumption)} metricHeader="Real" extra={(v) => `${formatConsumption(v.expectedConsumption)} (${formatDeviation(v.variancePercent)})`} extraHeader="Esperado" />
          </SimpleGrid>

          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
            <Section title="Por combustível" description={`Volume no período (${periodText})`}>
              {d.byFuelType.length === 0 ? <Text size="sm" c="dimmed">Nenhum abastecimento no período.</Text> : (
                <Table fz="sm">
                  <Table.Thead><Table.Tr><Table.Th>Combustível</Table.Th><Table.Th ta="right">Abast.</Table.Th><Table.Th ta="right">Quantidade</Table.Th>{d.canSeeCosts && <Table.Th ta="right">Custo</Table.Th>}</Table.Tr></Table.Thead>
                  <Table.Tbody>
                    {d.byFuelType.map((t) => (
                      <Table.Tr key={t.fuelTypeId}>
                        <Table.Td>{t.name}</Table.Td>
                        <Table.Td ta="right">{t.fuelings}</Table.Td>
                        <Table.Td ta="right">{formatQuantity(t.quantity, t.unit)}</Table.Td>
                        {d.canSeeCosts && <Table.Td ta="right">{formatCurrency(t.cost)}</Table.Td>}
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              )}
            </Section>
            <Section title="Últimos abastecimentos" action={<Button variant="default" size="xs" component={Link} to="/abastecimentos">Ver todos</Button>}>
              {d.recent.length === 0 ? <Text size="sm" c="dimmed">Nenhum abastecimento no período.</Text> : (
                <Stack gap={6}>
                  {d.recent.map((r) => (
                    <Group key={r.id} justify="space-between" gap="xs" wrap="nowrap">
                      <Anchor component={Link} to={`/abastecimentos/${r.id}`} size="sm" style={{ minWidth: 0 }} truncate>
                        <Text span ff="monospace" fw={600}>{formatPlate(r.licensePlate)}</Text> · {formatQuantity(r.quantity, r.unit)} · {formatDateTime(r.fueledAt)}
                      </Anchor>
                      <StatusBadge value={r.status} map={FUELING_STATUS} />
                    </Group>
                  ))}
                </Stack>
              )}
            </Section>
          </SimpleGrid>
          <Group justify="flex-end">
            <Button variant="subtle" leftSection={<IconChartBar size={16} />} component={Link} to={`/combustivel/relatorios?de=${period.from}&ate=${period.to}`}>Relatórios de combustível</Button>
          </Group>
        </Stack>
      )}
    </>
  );
}

/** Factual comparison: the metric and the period are always named — no "best/worst" labels (seção 48). */
function VehicleTable({ title, description, rows, metric, metricHeader, extra, extraHeader }: {
  title: string; description: string; rows: VehicleFuelMetric[];
  metric: (v: VehicleFuelMetric) => string; metricHeader: string; extra: (v: VehicleFuelMetric) => string; extraHeader: string;
}) {
  return (
    <Section title={title} description={description}>
      {rows.length === 0 ? <Text size="sm" c="dimmed">Nada a destacar no período.</Text> : (
        <Table.ScrollContainer minWidth={320}>
          <Table fz="sm">
            <Table.Thead><Table.Tr><Table.Th>Veículo</Table.Th><Table.Th ta="right">{metricHeader}</Table.Th><Table.Th ta="right">{extraHeader}</Table.Th></Table.Tr></Table.Thead>
            <Table.Tbody>
              {rows.map((v) => (
                <Table.Tr key={v.vehicleId}>
                  <Table.Td><Anchor component={Link} to={`/veiculos/${v.vehicleId}?aba=combustivel`} ff="monospace" fw={600} size="sm">{formatPlate(v.licensePlate)}</Anchor></Table.Td>
                  <Table.Td ta="right">{metric(v)}</Table.Td>
                  <Table.Td ta="right">{extra(v)}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Section>
  );
}
