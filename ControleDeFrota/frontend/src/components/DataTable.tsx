import type { KeyboardEvent, ReactNode } from 'react';
import { Card, Group, Pagination, Paper, Skeleton, Stack, Table, Text, UnstyledButton } from '@mantine/core';
import { useMediaQuery } from '@mantine/hooks';
import { IconChevronDown, IconChevronUp, IconSelector } from '@tabler/icons-react';
import type { PagedResult } from '../api/crud';
import type { SortState } from '../hooks/useListParams';
import { ErrorState, NoResultsState } from './States';
import classes from './DataTable.module.css';

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  /** Server-side sort key (whitelisted by the API). */
  sortKey?: string;
  /** Secondary columns are hidden on tablets to keep the table readable. */
  secondary?: boolean;
  align?: 'left' | 'right' | 'center';
  width?: number | string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: PagedResult<T> | undefined;
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
  getRowId: (row: T) => string;
  sort: SortState;
  onSortChange: (sort: SortState) => void;
  onPageChange: (page: number) => void;
  onRowClick?: (row: T) => void;
  rowActions?: (row: T) => ReactNode;
  /** Card layout for phones; tables don't fit 375px screens. */
  renderCard: (row: T) => ReactNode;
  /** Shown when the list is empty without filters — explains and offers the first action. */
  emptyState: ReactNode;
  hasFilters: boolean;
  onClearFilters: () => void;
}

export function DataTable<T>(props: DataTableProps<T>) {
  const { columns, data, isLoading, error, onRetry, getRowId, sort, onSortChange, onPageChange, onRowClick, rowActions } = props;
  const isPhone = useMediaQuery('(max-width: 48em)');
  const isTablet = useMediaQuery('(max-width: 75em)');
  const visibleColumns = isTablet ? columns.filter((c) => !c.secondary) : columns;

  if (error && !data) return <Paper>{<ErrorState error={error} onRetry={onRetry} />}</Paper>;

  if (isLoading && !data) {
    return (
      <Paper p="md">
        <Stack gap="sm" aria-busy="true" aria-label="Carregando registros">
          {Array.from({ length: 6 }, (_, i) => (
            <Skeleton key={i} height={isPhone ? 72 : 36} radius="sm" />
          ))}
        </Stack>
      </Paper>
    );
  }

  if (data && data.totalCount === 0) {
    return <Paper>{props.hasFilters ? <NoResultsState onClear={props.onClearFilters} /> : props.emptyState}</Paper>;
  }

  const footer = data && (
    <Group justify="space-between" p="sm" gap="xs" className={classes.footer}>
      <Text size="sm" c="dimmed">
        {data.totalCount === 1 ? '1 registro' : `${data.totalCount.toLocaleString('pt-BR')} registros`}
      </Text>
      {data.totalPages > 1 && (
        <Pagination total={data.totalPages} value={data.page} onChange={onPageChange} size="sm" siblings={isPhone ? 0 : 1} />
      )}
    </Group>
  );

  if (isPhone) {
    return (
      <Stack gap="sm" style={{ opacity: isLoading ? 0.6 : 1 }}>
        {data?.items.map((row) => (
          <Card
            key={getRowId(row)}
            padding="md"
            {...rowInteraction(row, onRowClick)}
          >
            <Group justify="space-between" align="flex-start" wrap="nowrap" gap="xs">
              <div style={{ minWidth: 0, flex: 1 }}>{props.renderCard(row)}</div>
              {rowActions && <div onClick={(e) => e.stopPropagation()}>{rowActions(row)}</div>}
            </Group>
          </Card>
        ))}
        <Paper>{footer}</Paper>
      </Stack>
    );
  }

  return (
    <Paper style={{ overflow: 'hidden' }}>
      <Table.ScrollContainer minWidth={640}>
        <Table highlightOnHover={!!onRowClick} verticalSpacing="sm" horizontalSpacing="md" style={{ opacity: isLoading ? 0.6 : 1 }}>
          <Table.Thead className={classes.head}>
            <Table.Tr>
              {visibleColumns.map((column) => (
                <Table.Th key={column.key} style={{ width: column.width, textAlign: column.align }}>
                  {column.sortKey ? <SortHeader column={column} sort={sort} onSortChange={onSortChange} /> : column.header}
                </Table.Th>
              ))}
              {rowActions && <Table.Th style={{ width: 56 }} aria-label="Ações" />}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {data?.items.map((row) => (
              <Table.Tr
                key={getRowId(row)}
                {...rowInteraction(row, onRowClick)}
              >
                {visibleColumns.map((column) => (
                  <Table.Td key={column.key} style={{ textAlign: column.align }}>
                    {column.render(row)}
                  </Table.Td>
                ))}
                {rowActions && <Table.Td onClick={(e) => e.stopPropagation()}>{rowActions(row)}</Table.Td>}
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {footer}
    </Paper>
  );
}

/** Clickable rows also open with Enter, so keyboard users get the same shortcut. */
function rowInteraction<T>(row: T, onRowClick?: (row: T) => void) {
  if (!onRowClick) return {};
  return {
    className: classes.clickable,
    tabIndex: 0,
    onClick: () => onRowClick(row),
    onKeyDown: (e: KeyboardEvent) => {
      if (e.key === 'Enter') onRowClick(row);
    },
  };
}

function SortHeader<T>({ column, sort, onSortChange }: { column: Column<T>; sort: SortState; onSortChange: (s: SortState) => void }) {
  const active = sort.by === column.sortKey;
  const Icon = !active ? IconSelector : sort.direction === 'Asc' ? IconChevronUp : IconChevronDown;
  const next: SortState = { by: column.sortKey!, direction: active && sort.direction === 'Asc' ? 'Desc' : 'Asc' };
  return (
    <UnstyledButton
      onClick={() => onSortChange(next)}
      className={classes.sort}
      aria-label={`Ordenar por ${column.header}`}
      aria-sort={active ? (sort.direction === 'Asc' ? 'ascending' : 'descending') : 'none'}
    >
      <Group gap={4} wrap="nowrap" justify={column.align === 'right' ? 'flex-end' : 'flex-start'}>
        <span>{column.header}</span>
        <Icon size={14} stroke={1.75} opacity={active ? 1 : 0.45} />
      </Group>
    </UnstyledButton>
  );
}
