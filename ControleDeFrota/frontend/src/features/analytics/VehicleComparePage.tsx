import { ActionIcon, Alert, Badge, Group, Paper, Skeleton, Stack, Table, Text, Tooltip } from '@mantine/core';
import { IconArrowsDiff, IconX } from '@tabler/icons-react';
import { Link, useSearchParams } from 'react-router-dom';
import { VehiclePicker } from '../../components/EntityPickers';
import { ExportMenu } from '../../components/ExportMenu';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { formatCurrency, formatDate, formatNumber, formatPlate } from '../../lib/format';
import { InfoHint } from '../finance/components';
import { PeriodFilter, usePeriod } from '../fuel/components';
import { analyticsApi, type VehicleMetrics } from './analytics';

const MAX = 6;

interface Metric {
  label: string;
  value: (m: VehicleMetrics) => number | null;
  format: (v: number | null) => string;
  /** Lower is better (costs, downtime); higher is better (consumption km/l). */
  better: 'lower' | 'higher' | null;
}

const money = (v: number | null) => formatCurrency(v);
const count = (v: number | null) => (v === null ? '—' : formatNumber(v));

const METRICS: Metric[] = [
  { label: 'Km rodados', value: (m) => m.kmDriven, format: (v) => (v === null ? '—' : `${formatNumber(v)} km`), better: null },
  { label: 'Consumo médio (km/l)', value: (m) => m.averageConsumption, format: (v) => (v === null ? '—' : v.toLocaleString('pt-BR')), better: 'higher' },
  { label: 'Combustível (R$)', value: (m) => m.fuelCost, format: money, better: 'lower' },
  { label: 'Manutenção (R$)', value: (m) => m.maintenanceCost, format: money, better: 'lower' },
  { label: 'Pneus (R$)', value: (m) => m.tireCost, format: money, better: 'lower' },
  { label: 'Outras despesas (R$)', value: (m) => m.otherCost, format: money, better: 'lower' },
  { label: 'Custo total', value: (m) => m.totalCost, format: money, better: 'lower' },
  { label: 'Custo por km', value: (m) => m.costPerKm, format: (v) => (v === null ? '—' : formatCurrency(v)), better: 'lower' },
  { label: 'OS concluídas', value: (m) => m.workOrdersCompleted, format: count, better: 'lower' },
  { label: 'Tempo parado (h)', value: (m) => m.downtimeHours, format: (v) => (v === null ? '—' : `${v.toLocaleString('pt-BR')} h`), better: 'lower' },
  { label: 'Trocas de pneu', value: (m) => m.tireReplacements, format: count, better: 'lower' },
];

/** Variation against the fleet average, shown under each vehicle value (internal benchmark, spec §18). */
function vsAverage(value: number | null, average: number | null) {
  if (value === null || average === null || average === 0) return null;
  return Math.round(((value - average) / average) * 100);
}

export function VehicleComparePage() {
  const [params, setParams] = useSearchParams();
  const period = usePeriod(90);
  const ids = (params.get('ids') ?? '').split(',').filter(Boolean).slice(0, MAX);
  const query = analyticsApi.useCompare(ids, period.from, period.to);
  const data = query.data;
  const fleet = data?.benchmarks.find((b) => b.label === 'Média da frota')?.average ?? null;

  const setIds = (next: string[]) =>
    setParams((p) => { const n = new URLSearchParams(p); if (next.length) n.set('ids', next.join(',')); else n.delete('ids'); return n; }, { replace: true });

  const columns = data ? [...data.vehicles.map((v) => ({ key: v.vehicleId, title: formatPlate(v.licensePlate), sub: v.model, metrics: v, removable: true })),
    ...data.benchmarks.map((b) => ({ key: b.label, title: b.label, sub: `${b.vehicleCount} veículo(s)`, metrics: b.average, removable: false }))] : [];

  return (
    <>
      <PageHeader title="Comparar veículos" breadcrumbs={[{ label: 'Relatórios', to: '/relatorios' }, { label: 'Comparar veículos' }]}
        description={`Escolha até ${MAX} veículos. A comparação inclui a média da frota e a média dos veículos do mesmo tipo.`}
        action={data && (
          <ExportMenu source={{
            title: 'Comparação de veículos', subtitle: `Período ${formatDate(data.from)} a ${formatDate(data.to)}`,
            columns: [{ header: 'Indicador', value: (m: Metric) => m.label },
              ...columns.map((c) => ({ header: c.title, value: (m: Metric) => m.value(c.metrics) }))],
            load: async () => ({ rows: METRICS, truncated: false }),
          }} />
        )} />
      <PeriodFilter period={period} />
      <Paper p="sm" mb="md">
        <Group gap="sm" wrap="wrap">
          <VehiclePicker aria-label="Adicionar veículo" placeholder={ids.length >= MAX ? `Máximo de ${MAX} veículos` : 'Adicionar veículo à comparação'}
            value={null} disabled={ids.length >= MAX} onChange={(v) => v && !ids.includes(v) && setIds([...ids, v])} style={{ flex: '1 1 260px', maxWidth: 360 }} />
          {data?.vehicles.map((v) => (
            <Badge key={v.vehicleId} size="lg" variant="light" rightSection={
              <ActionIcon size="xs" variant="transparent" aria-label={`Remover ${formatPlate(v.licensePlate)}`} onClick={() => setIds(ids.filter((i) => i !== v.vehicleId))}><IconX size={12} /></ActionIcon>
            }>{formatPlate(v.licensePlate)}</Badge>
          ))}
        </Group>
      </Paper>
      {ids.length === 0 ? (
        <Paper><EmptyState icon={<IconArrowsDiff size={28} />} title="Escolha os veículos" description="Adicione dois ou mais veículos para comparar consumo, custos, manutenção, pneus e tempo parado no período." /></Paper>
      ) : query.error && !data ? (
        <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper>
      ) : !data ? <Skeleton height={360} /> : (
        <Stack gap="sm">
          {data.vehicles.some((v) => v.isPartial) && (
            <Alert color="orange" variant="light">Totais parciais: você não tem permissão de custo para todas as fontes.</Alert>
          )}
          <Paper>
            <Table.ScrollContainer minWidth={260 + columns.length * 150}>
              <Table striped fz="sm" style={{ opacity: query.isFetching ? 0.6 : 1 }}>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Indicador</Table.Th>
                    {columns.map((c) => (
                      <Table.Th key={c.key} ta="right" style={{ background: c.removable ? undefined : 'var(--mantine-color-default-hover)' }}>
                        {c.removable ? <Link to={`/veiculos/${c.key}`}>{c.title}</Link> : c.title}
                        <Text size="xs" c="dimmed" fw={400}>{c.sub}</Text>
                      </Table.Th>
                    ))}
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {METRICS.map((m) => (
                    <Table.Tr key={m.label}>
                      <Table.Td fw={500}>{m.label}</Table.Td>
                      {columns.map((c) => {
                        const value = m.value(c.metrics);
                        const diff = c.removable && fleet ? vsAverage(value, m.value(fleet)) : null;
                        const worse = diff !== null && m.better && (m.better === 'lower' ? diff >= 15 : diff <= -15);
                        return (
                          <Table.Td key={c.key} ta="right" style={{ background: c.removable ? undefined : 'var(--mantine-color-default-hover)' }}>
                            {m.format(value)}
                            {diff !== null && diff !== 0 && (
                              <Tooltip label="Diferença em relação à média da frota no mesmo período">
                                <Text size="xs" c={worse ? 'red' : 'dimmed'}>{diff > 0 ? '+' : ''}{diff}% vs frota</Text>
                              </Tooltip>
                            )}
                          </Table.Td>
                        );
                      })}
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Paper>
          <Group gap={4}>
            <Text size="xs" c="dimmed">"—" = sem dado no período ou sem permissão. Médias consideram só os veículos que têm o dado.</Text>
            <InfoHint label="Como comparar">Os números vêm dos mesmos cálculos dos relatórios. Em vermelho, diferenças de 15% ou mais para pior em relação à média da frota — um ponto para investigar, não uma conclusão.</InfoHint>
          </Group>
        </Stack>
      )}
    </>
  );
}
