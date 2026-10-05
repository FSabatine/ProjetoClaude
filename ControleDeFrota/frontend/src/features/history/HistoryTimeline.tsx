import { useState } from 'react';
import { Group, Pagination, Select, Skeleton, Stack, Text, ThemeIcon, Timeline } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import {
  IconAlertTriangle,
  IconChecklist,
  IconCircleDot,
  IconCoin,
  IconGasStation,
  IconTool,
  IconFileText,
  IconGauge,
  IconHistory,
  IconRefresh,
  IconSteeringWheel,
  IconTruck,
} from '@tabler/icons-react';
import { toSelectData } from '../../components/common';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime, toApiDate } from '../../lib/format';
import { ALERT_CATEGORY } from '../alerts/alerts';
import { historyApi } from '../operations/api';

/** Icon and color by event family — the summary text itself comes from the API (frozen at event time). */
function visual(type: string) {
  if (type.startsWith('VehicleAssign')) return { icon: IconSteeringWheel, color: 'cyan' };
  if (type === 'VehicleStatusChanged') return { icon: IconTruck, color: 'blue' };
  if (type === 'MileageAnomalyDetected') return { icon: IconGauge, color: 'orange' };
  if (type.startsWith('Mileage')) return { icon: IconGauge, color: 'teal' };
  if (type === 'DocumentExpired') return { icon: IconFileText, color: 'red' };
  if (type.startsWith('Document')) return { icon: IconFileText, color: 'grape' };
  if (type === 'ChecklistFailed') return { icon: IconChecklist, color: 'red' };
  if (type.startsWith('Checklist')) return { icon: IconChecklist, color: 'teal' };
  if (type === 'OccurrenceStatusChanged') return { icon: IconRefresh, color: 'blue' };
  if (type.startsWith('Occurrence')) return { icon: IconAlertTriangle, color: 'orange' };
  if (type === 'TireInspectionFailed' || type === 'TireTreadLow' || type === 'TirePressureLow' || type === 'TireAnomalyDetected')
    return { icon: IconAlertTriangle, color: 'orange' };
  if (type === 'TireEndOfLife') return { icon: IconCircleDot, color: 'gray' };
  if (type.startsWith('Tire')) return { icon: IconCircleDot, color: 'indigo' };
  if (type.startsWith('Fuel')) return { icon: IconGasStation, color: type.includes('Anomaly') || type.includes('Review') ? 'orange' : 'teal' };
  if (type.startsWith('WorkOrder') || type.startsWith('Maintenance') || type.startsWith('HourMeter')) return { icon: IconTool, color: 'orange' };
  if (type.startsWith('Expense') || type.startsWith('Budget') || type.startsWith('RecurringExpense')) return { icon: IconCoin, color: 'grape' };
  return { icon: IconHistory, color: 'gray' };
}

/** Operational timeline of a vehicle or driver (ADR-025), newest first, with an optional period. */
export function HistoryTimeline({ owner, id }: { owner: 'vehicles' | 'drivers' | 'tires'; id: string }) {
  const [from, setFrom] = useState<Date | null>(null);
  const [to, setTo] = useState<Date | null>(null);
  const [page, setPage] = useState(1);
  const [category, setCategory] = useState<string | null>(null);
  const query = historyApi.useFor(owner, id, { from: toApiDate(from), to: toApiDate(to), category, page, pageSize: 25 });

  return (
    <Stack gap="md">
      <Group gap="sm" wrap="wrap">
        <DateInput label="De" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable value={from} onChange={(v) => { setFrom(v); setPage(1); }} maxDate={to ?? new Date()} w={170} />
        <DateInput label="Até" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable value={to} onChange={(v) => { setTo(v); setPage(1); }} minDate={from ?? undefined} maxDate={new Date()} w={170} />
        {owner !== 'tires' && (
          <Select label="Área" placeholder="Todas" data={toSelectData(ALERT_CATEGORY)} clearable value={category}
            onChange={(v) => { setCategory(v); setPage(1); }} w={190} />
        )}
      </Group>
      <Text size="xs" c="dimmed" mt={-8}>Tudo o que aconteceu, de todos os módulos, em ordem — só aparecem os assuntos que você tem permissão para ver.</Text>

      {query.isLoading && <Stack>{[1, 2, 3, 4].map((i) => <Skeleton key={i} height={48} />)}</Stack>}
      {query.error && !query.data && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.totalCount === 0 && (
        <EmptyState
          icon={<IconHistory size={28} />}
          title={from || to || category ? 'Nada com estes filtros' : 'Nenhum evento ainda'}
          description={owner === 'tires'
            ? 'Cadastro, instalações, remoções, rodízios, inspeções, consertos e recapagens aparecem aqui conforme acontecem.'
            : 'Alocações, hodômetro, documentos, checklists, ocorrências, manutenções, abastecimentos, pneus e despesas aparecem aqui conforme acontecem.'}
        />
      )}
      {query.data && query.data.totalCount > 0 && (
        <>
          <Timeline bulletSize={28} lineWidth={2} style={{ opacity: query.isFetching ? 0.6 : 1 }}>
            {query.data.items.map((e) => {
              const { icon: Icon, color } = visual(e.type);
              return (
                <Timeline.Item key={e.id} bullet={<ThemeIcon size={28} radius="xl" color={color} variant="light"><Icon size={16} /></ThemeIcon>}>
                  <Text size="sm">{e.summary}</Text>
                  <Text size="xs" c="dimmed">{formatDateTime(e.occurredAt)} · {e.userName ?? 'Sistema'}</Text>
                </Timeline.Item>
              );
            })}
          </Timeline>
          {query.data.totalPages > 1 && <Pagination total={query.data.totalPages} value={page} onChange={setPage} size="sm" />}
        </>
      )}
    </Stack>
  );
}
