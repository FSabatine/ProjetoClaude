import { useState } from 'react';
import { Group, Pagination, Skeleton, Stack, Text, ThemeIcon, Timeline } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import {
  IconAlertTriangle,
  IconChecklist,
  IconFileText,
  IconGauge,
  IconHistory,
  IconRefresh,
  IconSteeringWheel,
  IconTruck,
} from '@tabler/icons-react';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime, toApiDate } from '../../lib/format';
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
  return { icon: IconHistory, color: 'gray' };
}

/** Operational timeline of a vehicle or driver (ADR-025), newest first, with an optional period. */
export function HistoryTimeline({ owner, id }: { owner: 'vehicles' | 'drivers'; id: string }) {
  const [from, setFrom] = useState<Date | null>(null);
  const [to, setTo] = useState<Date | null>(null);
  const [page, setPage] = useState(1);
  const query = historyApi.useFor(owner, id, { from: toApiDate(from), to: toApiDate(to), page, pageSize: 25 });

  return (
    <Stack gap="md">
      <Group gap="sm" wrap="wrap">
        <DateInput label="De" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable value={from} onChange={(v) => { setFrom(v); setPage(1); }} maxDate={to ?? new Date()} w={170} />
        <DateInput label="Até" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable value={to} onChange={(v) => { setTo(v); setPage(1); }} minDate={from ?? undefined} maxDate={new Date()} w={170} />
      </Group>

      {query.isLoading && <Stack>{[1, 2, 3, 4].map((i) => <Skeleton key={i} height={48} />)}</Stack>}
      {query.error && !query.data && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.totalCount === 0 && (
        <EmptyState
          icon={<IconHistory size={28} />}
          title={from || to ? 'Nada no período' : 'Nenhum evento ainda'}
          description="Alocações, leituras de hodômetro, documentos, checklists e ocorrências aparecem aqui conforme acontecem."
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
