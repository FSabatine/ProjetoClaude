import { Anchor, Group, Stack, Text } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { IconFileText } from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import { ListToolbar, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatDate, fromApiDate, toApiDate } from '../../lib/format';
import { dueLabel } from '../../lib/mileage';
import { documentTypesApi, documentsApi, type DocumentListItem } from '../operations/api';
import { DOCUMENT_OWNER_TYPE, DOCUMENT_STATUS, type DocumentOwnerType } from '../operations/labels';

const FILTERS = ['status', 'ownerType', 'documentTypeId', 'expiresFrom', 'expiresTo', 'alertsOnly'] as const;

/** Where each owner's documents live (the hub page, documents tab). */
export const documentOwnerLink = (ownerType: DocumentOwnerType, ownerId: string | null) => {
  switch (ownerType) {
    case 'Vehicle': return `/veiculos/${ownerId}?aba=documentos`;
    case 'Driver': return `/motoristas/${ownerId}?aba=documentos`;
    case 'Implement': return `/implementos/${ownerId}`;
    default: return '/minha-empresa';
  }
};

/** All fleet documents in one place, focused on what is expiring. Editing happens on the owner's page. */
export function DocumentListPage() {
  const navigate = useNavigate();
  const list = useListParams(FILTERS, { by: 'expiresOn', direction: 'Asc' });
  const query = documentsApi.useList(list.apiParams);
  const types = documentTypesApi.useList((list.filters.ownerType as DocumentOwnerType | null) ?? null, true);

  const columns: Column<DocumentListItem>[] = [
    {
      key: 'type', header: 'Documento', sortKey: 'type', render: (d) => (
        <div>
          <Text size="sm" fw={600}>{d.documentTypeName}</Text>
          {d.number && <Text size="xs" c="dimmed">Nº {d.number}</Text>}
        </div>
      ),
    },
    {
      key: 'owner', header: 'De', render: (d) => (
        <div>
          <Anchor component={Link} to={documentOwnerLink(d.ownerType, d.ownerId)} size="sm" onClick={(e) => e.stopPropagation()}>{d.ownerName ?? '—'}</Anchor>
          <Text size="xs" c="dimmed">{DOCUMENT_OWNER_TYPE[d.ownerType].label}</Text>
        </div>
      ),
    },
    {
      key: 'expires', header: 'Vencimento', sortKey: 'expiresOn', render: (d) => (
        <div>
          <Text size="sm">{formatDate(d.expiresOn)}</Text>
          {d.daysUntilExpiration !== null && d.status !== 'Replaced' && <Text size="xs" c="dimmed">{dueLabel(d.daysUntilExpiration)}</Text>}
        </div>
      ),
    },
    { key: 'status', header: 'Situação', render: (d) => <StatusBadge value={d.status} map={DOCUMENT_STATUS} /> },
  ];

  return (
    <>
      <PageHeader title="Documentos" description="Documentos de veículos, motoristas, implementos e da empresa, com os vencimentos." />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por número, tipo, placa ou motorista"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(DOCUMENT_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'ownerType', placeholder: 'De', data: toSelectData(DOCUMENT_OWNER_TYPE), value: list.filters.ownerType, onChange: (v) => list.setFilter('ownerType', v) },
          {
            key: 'documentTypeId', placeholder: 'Tipo', value: list.filters.documentTypeId, onChange: (v) => list.setFilter('documentTypeId', v),
            data: (types.data ?? []).map((t) => ({ value: t.id, label: `${t.name} (${DOCUMENT_OWNER_TYPE[t.ownerType].label})` })),
          },
        ]}
      />
      <Group gap="sm" mb="md" mt={-8}>
        <DateInput size="xs" label="Vence a partir de" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable w={160}
          value={fromApiDate(list.filters.expiresFrom)} onChange={(v) => list.setFilter('expiresFrom', toApiDate(v))} />
        <DateInput size="xs" label="Vence até" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable w={160}
          value={fromApiDate(list.filters.expiresTo)} onChange={(v) => list.setFilter('expiresTo', toApiDate(v))} />
      </Group>
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(d) => d.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(d) => navigate(documentOwnerLink(d.ownerType, d.ownerId))}
        renderCard={(d) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={600} size="sm">{d.documentTypeName}</Text>
              <StatusBadge value={d.status} map={DOCUMENT_STATUS} />
            </Group>
            <Text size="sm">{d.ownerName} · {DOCUMENT_OWNER_TYPE[d.ownerType].label}</Text>
            <Text size="xs" c="dimmed">{d.expiresOn ? `${formatDate(d.expiresOn)} · ${dueLabel(d.daysUntilExpiration)}` : 'Sem vencimento'}</Text>
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconFileText size={28} />} title="Nenhum documento cadastrado"
            description="Cadastre os documentos na página de cada veículo, motorista ou implemento. Eles aparecerão aqui com os vencimentos." />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
