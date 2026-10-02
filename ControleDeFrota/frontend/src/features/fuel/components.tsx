import type { ReactNode } from 'react';
import { ActionIcon, Alert, Group, Paper, Popover, SegmentedControl, Stack, Text } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconAlertTriangle, IconCircleCheck, IconHelpCircle } from '@tabler/icons-react';
import dayjs from 'dayjs';
import { useSearchParams } from 'react-router-dom';
import { formatDateTime } from '../../lib/format';
import { formatConsumption, formatDeviation, litersPer100Km, type FuelUnit } from '../../lib/fuel';
import { BASELINE_SOURCE, CONSUMPTION_RESULT, FUEL_ANOMALY_TYPE, type BaselineSource, type ConsumptionResult, type FuelingAnomaly } from './fuel';

/** Contextual "[?]" (seção 49): a short plain-language explanation next to a number — no technical details. */
export function InfoHint({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Popover width={300} position="bottom-start" withArrow shadow="md">
      <Popover.Target>
        <ActionIcon variant="subtle" color="gray" size="sm" aria-label={`O que é ${label}?`}>
          <IconHelpCircle size={15} />
        </ActionIcon>
      </Popover.Target>
      <Popover.Dropdown>
        <Text size="sm" fw={600} mb={4}>{label}</Text>
        <Text size="sm" c="dimmed" component="div">{children}</Text>
      </Popover.Dropdown>
    </Popover>
  );
}

export const CONSUMPTION_HELP = (
  <>
    O consumo é calculado de <b>tanque cheio a tanque cheio</b>: os quilômetros rodados entre dois abastecimentos completos, divididos
    por todo o combustível colocado nesse intervalo (incluindo os abastecimentos parciais). Por isso o primeiro tanque cheio ainda não
    tem consumo, e um abastecimento parcial entra na conta do próximo tanque cheio.
  </>
);

export const COST_PER_KM_HELP = (
  <>Quanto se gastou de combustível para rodar cada quilômetro, considerando apenas os trechos em que o consumo pôde ser medido.</>
);

export function KpiTile({ label, value, hint, help }: { label: string; value: ReactNode; hint?: ReactNode; help?: ReactNode }) {
  return (
    <Paper p="md">
      <Group gap={2} wrap="nowrap">
        <Text size="xs" c="dimmed" fw={500} tt="uppercase">{label}</Text>
        {help && <InfoHint label={label}>{help}</InfoHint>}
      </Group>
      <Text fw={650} fz={{ base: 20, sm: 24 }} mt={2}>{value}</Text>
      {hint && <Text size="xs" c="dimmed">{hint}</Text>}
    </Paper>
  );
}

/** The consumption of a fueling, or why there is none (never a silent blank). */
export function ConsumptionDetails({ result, consumption, unit, expected, baselineSource, deviation, distanceKm, quantity }: {
  result: ConsumptionResult;
  consumption: number | null;
  unit: FuelUnit;
  expected: number | null;
  baselineSource: BaselineSource | null;
  deviation: number | null;
  distanceKm: number | null;
  quantity: number | null;
}) {
  if (result !== 'Calculated' || consumption === null) return <Text size="sm" c="dimmed">{CONSUMPTION_RESULT[result].label}.</Text>;
  const per100 = unit === 'Liter' ? litersPer100Km(consumption) : null;
  return (
    <Stack gap={2}>
      <Text size="sm">
        <b>{formatConsumption(consumption, unit)}</b>
        {per100 !== null && <Text span c="dimmed" size="sm"> · {per100.toLocaleString('pt-BR')} L/100 km</Text>}
      </Text>
      <Text size="xs" c="dimmed">
        {distanceKm?.toLocaleString('pt-BR')} km com {quantity?.toLocaleString('pt-BR')} {unit === 'Liter' ? 'L' : unit === 'CubicMeter' ? 'm³' : 'kWh'} desde o último tanque cheio
      </Text>
      {expected !== null && (
        <Text size="xs" c="dimmed">
          Esperado: {formatConsumption(expected, unit)} ({baselineSource ? BASELINE_SOURCE[baselineSource].label : '—'}) · diferença {formatDeviation(deviation)}
        </Text>
      )}
    </Stack>
  );
}

/** Warnings of a fueling — neutral wording, with who reviewed them. */
export function AnomalyList({ anomalies }: { anomalies: FuelingAnomaly[] }) {
  if (anomalies.length === 0) return null;
  return (
    <Stack gap="xs">
      {anomalies.map((a) => (
        <Alert key={a.id} color={a.reviewedAt ? 'gray' : 'orange'} p="xs"
          icon={a.reviewedAt ? <IconCircleCheck size={18} /> : <IconAlertTriangle size={18} />}
          title={<Text size="sm" fw={600}>{FUEL_ANOMALY_TYPE[a.type].label}{a.reviewedAt ? ' · revisado' : ' · requer revisão'}</Text>}>
          <Text size="sm">{a.message}</Text>
          {a.reviewedAt && <Text size="xs" c="dimmed">Revisado por {a.reviewedByName ?? '—'} em {formatDateTime(a.reviewedAt)}</Text>}
        </Alert>
      ))}
    </Stack>
  );
}

const PRESETS = { '30': 'Últimos 30 dias', '90': '90 dias', '365': '12 meses' } as const;

/** Period in the URL (?de=&ate=) so a dashboard/report view can be shared and survives the back button. */
export function usePeriod(defaultDays = 30) {
  const [params, setParams] = useSearchParams();
  const today = dayjs().startOf('day');
  const to = params.get('ate') ?? today.format('YYYY-MM-DD');
  const from = params.get('de') ?? today.subtract(defaultDays - 1, 'day').format('YYYY-MM-DD');
  const set = (nextFrom: string, nextTo: string) =>
    setParams((p) => { const n = new URLSearchParams(p); n.set('de', nextFrom); n.set('ate', nextTo); n.delete('page'); return n; }, { replace: true });
  return { from, to, set };
}

export function PeriodFilter({ period }: { period: ReturnType<typeof usePeriod> }) {
  const today = dayjs().format('YYYY-MM-DD');
  const preset = period.to === today
    ? (Object.keys(PRESETS) as (keyof typeof PRESETS)[]).find((d) => dayjs(today).subtract(Number(d) - 1, 'day').format('YYYY-MM-DD') === period.from)
    : undefined;
  return (
    <Paper p="sm" mb="md">
      <Group gap="sm" wrap="wrap">
        <SegmentedControl
          value={preset ?? ''}
          onChange={(d) => period.set(dayjs(today).subtract(Number(d) - 1, 'day').format('YYYY-MM-DD'), today)}
          data={Object.entries(PRESETS).map(([value, label]) => ({ value, label }))}
          aria-label="Período rápido"
        />
        <DatePickerInput
          type="range"
          aria-label="Período"
          valueFormat="DD/MM/YYYY"
          maxDate={new Date()}
          value={[dayjs(period.from).toDate(), dayjs(period.to).toDate()]}
          onChange={([start, end]) => {
            if (start && end) period.set(dayjs(start).format('YYYY-MM-DD'), dayjs(end).format('YYYY-MM-DD'));
          }}
          style={{ flex: '1 1 240px', maxWidth: 300 }}
        />
      </Group>
    </Paper>
  );
}
