import { useState, type ReactNode } from 'react';
import { Group, Pagination, Skeleton, Table, Text, UnstyledButton } from '@mantine/core';
import { IconChevronDown, IconChevronUp, IconSelector } from '@tabler/icons-react';
import { ErrorState } from './States';

// Server-side sorted and paged report tables (fuel and tire reports).

export interface ReportColumn<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  sortKey?: string;
  align?: 'right';
}

/** Sort + page for one report; kept local (each tab has its own) and sent to the server. */
export function useReportState(defaultSort: string, defaultDirection: 'Asc' | 'Desc' = 'Asc') {
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState({ by: defaultSort, direction: defaultDirection });
  return { page, setPage, sort, setSort: (s: typeof sort) => { setSort(s); setPage(1); }, params: { page, pageSize: 20, sortBy: sort.by, sortDirection: sort.direction } };
}

export function ReportTable<T>({ columns, data, isLoading, error, onRetry, getKey, state, empty }: {
  columns: ReportColumn<T>[];
  data: { items: T[]; page: number; totalPages: number; totalCount: number } | undefined;
  isLoading: boolean; error: unknown; onRetry: () => void; getKey: (row: T) => string;
  state: ReturnType<typeof useReportState>; empty: string;
}) {
  if (error && !data) return <ErrorState error={error} onRetry={onRetry} />;
  if (!data) return <Skeleton height={240} />;
  if (data.totalCount === 0) return <Text size="sm" c="dimmed" p="md">{empty}</Text>;
  return (
    <>
      <Table.ScrollContainer minWidth={640}>
        <Table striped highlightOnHover fz="sm" style={{ opacity: isLoading ? 0.6 : 1 }}>
          <Table.Thead>
            <Table.Tr>
              {columns.map((c) => (
                <Table.Th key={c.key} ta={c.align}>
                  {c.sortKey ? <SortButton column={c} state={state} /> : c.header}
                </Table.Th>
              ))}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {data.items.map((row) => (
              <Table.Tr key={getKey(row)}>{columns.map((c) => <Table.Td key={c.key} ta={c.align}>{c.render(row)}</Table.Td>)}</Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      <Group justify="space-between" p="sm">
        <Text size="sm" c="dimmed">{data.totalCount.toLocaleString('pt-BR')} linha(s)</Text>
        {data.totalPages > 1 && <Pagination size="sm" total={data.totalPages} value={data.page} onChange={state.setPage} />}
      </Group>
    </>
  );
}

function SortButton<T>({ column, state }: { column: ReportColumn<T>; state: ReturnType<typeof useReportState> }) {
  const active = state.sort.by === column.sortKey;
  const Icon = !active ? IconSelector : state.sort.direction === 'Asc' ? IconChevronUp : IconChevronDown;
  return (
    <UnstyledButton aria-label={`Ordenar por ${column.header}`} fz="sm" fw={600}
      onClick={() => state.setSort({ by: column.sortKey!, direction: active && state.sort.direction === 'Asc' ? 'Desc' : 'Asc' })}>
      <Group gap={4} wrap="nowrap" justify={column.align === 'right' ? 'flex-end' : 'flex-start'}>{column.header}<Icon size={14} opacity={active ? 1 : 0.45} /></Group>
    </UnstyledButton>
  );
}
