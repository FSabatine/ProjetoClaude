import { useState } from 'react';
import { Alert, Anchor, Button, Group, Paper, SegmentedControl, SimpleGrid, Skeleton, Stack, Table, Text } from '@mantine/core';
import { IconAlertTriangle, IconPlus } from '@tabler/icons-react';
import dayjs from 'dayjs';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ColumnChart } from '../../components/ColumnChart';
import { StatusBadge } from '../../components/common';
import { Section } from '../../components/DetailLayout';
import { ErrorState } from '../../components/States';
import { formatCurrency, formatDateTime, formatNumber } from '../../lib/format';
import { FUEL_UNIT_SYMBOL, formatConsumption, formatDeviation, formatQuantity } from '../../lib/fuel';
import { fuelAnalyticsApi, fuelingsApi } from './api';
import { COST_PER_KM_HELP, CONSUMPTION_HELP, KpiTile } from './components';
import { BASELINE_SOURCE, FUELING_STATUS, HISTORY_GRANULARITY, type HistoryGranularity } from './fuel';

/** Range that keeps each granularity readable (a daily chart over a year would be noise). */
const RANGE_DAYS: Record<HistoryGranularity, number> = { Day: 30, Week: 120, Month: 365 };

/** "Combustível" tab of the vehicle hub: summary first, then history, costs and the fuelings themselves (seção 26). */
export function VehicleFuelPanel({ vehicleId, vehicleInactive }: { vehicleId: string; vehicleInactive: boolean }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const [granularity, setGranularity] = useState<HistoryGranularity>('Month');
  const to = dayjs().format('YYYY-MM-DD');
  const from = dayjs().subtract(RANGE_DAYS[granularity] - 1, 'day').format('YYYY-MM-DD');
  const query = fuelAnalyticsApi.useVehicle(vehicleId, { granularity, from, to });
  const fuelings = fuelingsApi.useList({ vehicleId, pageSize: 10, sortBy: 'fueledAt', sortDirection: 'Desc' });
  const d = query.data;

  const add = can(PERMISSIONS.fuel.create) && !vehicleInactive && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate(`/abastecimentos/novo?veiculo=${vehicleId}`)}>Registrar abastecimento</Button>
  );

  if (query.error && !d) return <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper>;
  if (!d) return <Skeleton height={320} />;
  const unit = d.unit;
  const deviation = d.totals.averageConsumption !== null && d.expectedConsumption
    ? ((d.totals.averageConsumption - d.expectedConsumption) / d.expectedConsumption) * 100 : null;

  return (
    <Stack gap="md">
      <Group justify="space-between" gap="sm">
        <SegmentedControl value={granularity} onChange={(v) => setGranularity(v as HistoryGranularity)}
          data={Object.entries(HISTORY_GRANULARITY).map(([value, { label }]) => ({ value, label }))} aria-label="Agrupar histórico" />
        {add}
      </Group>
      {d.pendingReview > 0 && (
        <Alert color="orange" icon={<IconAlertTriangle size={18} />} p="xs">
          <Anchor component={Link} to={`/abastecimentos?vehicleId=${vehicleId}&status=PendingReview`} size="sm" c="inherit">
            {d.pendingReview} abastecimento(s) deste veículo aguardando revisão.
          </Anchor>
        </Alert>
      )}

      <SimpleGrid cols={{ base: 2, md: 4 }} spacing="sm">
        <KpiTile label="Consumo médio" value={formatConsumption(d.totals.averageConsumption, unit)} help={CONSUMPTION_HELP}
          hint={d.expectedConsumption ? `esperado ${formatConsumption(d.expectedConsumption, unit)} (${formatDeviation(deviation)})` : 'sem referência ainda'} />
        {d.canSeeCosts && <KpiTile label="Custo/km" value={formatCurrency(d.totals.costPerKm)} help={COST_PER_KM_HELP} hint={`${formatNumber(d.totals.measuredDistanceKm)} km medidos`} />}
        {d.canSeeCosts && <KpiTile label="Custo no período" value={formatCurrency(d.totals.totalCost)} hint={`${d.totals.fuelings} abastecimento(s)`} />}
        <KpiTile label={unit === 'Liter' ? 'Litros' : `Quantidade (${FUEL_UNIT_SYMBOL[unit]})`} value={formatNumber(Math.round(unit === 'Liter' ? d.totals.liters : d.totals.otherUnitsQuantity))}
          hint={d.tankCapacity ? `tanque: ${formatNumber(d.tankCapacity)} ${FUEL_UNIT_SYMBOL[unit]}` : 'capacidade do tanque não cadastrada'} />
      </SimpleGrid>
      {d.expectedConsumption && d.baselineSource && (
        <Text size="xs" c="dimmed">Referência de consumo: {BASELINE_SOURCE[d.baselineSource].label}. Período: últimos {RANGE_DAYS[granularity]} dias.</Text>
      )}

      <SimpleGrid cols={{ base: 1, md: d.canSeeCosts ? 2 : 1 }} spacing="md">
        <ColumnChart title={`Consumo (km/${FUEL_UNIT_SYMBOL[unit]})`} description={HISTORY_GRANULARITY[granularity].label}
          points={d.history.map((p) => ({ key: p.start, label: p.label, value: p.consumption }))}
          format={(v) => formatConsumption(v, unit)} emptyText="Ainda não há trechos medidos neste período. O consumo aparece a partir do segundo tanque cheio." />
        {d.canSeeCosts && (
          <ColumnChart title="Gasto com combustível" description={HISTORY_GRANULARITY[granularity].label}
            points={d.history.map((p) => ({ key: p.start, label: p.label, value: p.cost && p.cost > 0 ? p.cost : null }))}
            format={(v) => formatCurrency(v)} />
        )}
      </SimpleGrid>

      <Section title="Abastecimentos" action={<Button variant="default" size="xs" component={Link} to={`/abastecimentos?vehicleId=${vehicleId}`}>Ver todos</Button>}>
        {(fuelings.data?.items.length ?? 0) === 0 ? <Text size="sm" c="dimmed">Nenhum abastecimento registrado para este veículo.</Text> : (
          <Table.ScrollContainer minWidth={560}>
            <Table highlightOnHover fz="sm">
              <Table.Thead>
                <Table.Tr><Table.Th>Data</Table.Th><Table.Th ta="right">Hodômetro</Table.Th><Table.Th>Combustível</Table.Th><Table.Th ta="right">Quantidade</Table.Th>
                  {d.canSeeCosts && <Table.Th ta="right">Total</Table.Th>}<Table.Th ta="right">Consumo</Table.Th><Table.Th>Situação</Table.Th></Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {fuelings.data!.items.map((f) => (
                  <Table.Tr key={f.id} style={{ cursor: 'pointer' }} onClick={() => navigate(`/abastecimentos/${f.id}`)}>
                    <Table.Td>{formatDateTime(f.fueledAt)}</Table.Td>
                    <Table.Td ta="right">{formatNumber(f.odometerKm)} km</Table.Td>
                    <Table.Td>{f.fuelTypeName}{!f.isFullTank && <Text span size="xs" c="dimmed"> · parcial</Text>}</Table.Td>
                    <Table.Td ta="right">{formatQuantity(f.quantity, f.unit)}</Table.Td>
                    {d.canSeeCosts && <Table.Td ta="right">{formatCurrency(f.totalAmount)}</Table.Td>}
                    <Table.Td ta="right">{formatConsumption(f.consumption, f.unit)}</Table.Td>
                    <Table.Td><StatusBadge value={f.status} map={FUELING_STATUS} /></Table.Td>
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
