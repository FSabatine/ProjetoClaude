import { Button, Group, Stack, Text } from '@mantine/core';
import { IconPlus, IconTruckLoading } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, RowActions, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatNumber, formatPlate } from '../../lib/format';
import { CAPACITY_UNIT, IMPLEMENT_STATUS, IMPLEMENT_TYPE, implementsApi, type ImplementListItem } from './implements';

const FILTERS = ['status', 'type'] as const;

const capacity = (i: ImplementListItem) => (i.capacity === null ? '—' : `${formatNumber(i.capacity)} ${i.capacityUnit ? CAPACITY_UNIT[i.capacityUnit].label : ''}`);

export function ImplementListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'licensePlate', direction: 'Asc' });
  const query = implementsApi.useList(list.apiParams);
  const remove = implementsApi.useRemove();

  const newButton = can(PERMISSIONS.implements.create) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/implementos/novo')}>
      Novo implemento
    </Button>
  );

  const handleDelete = (i: ImplementListItem) =>
    confirmDelete({
      entity: 'implemento',
      name: `${formatPlate(i.licensePlate)} — ${IMPLEMENT_TYPE[i.type].label} ${i.model}`,
      onConfirm: () =>
        remove.mutate(i.id, {
          onSuccess: () => notifySuccess('Implemento excluído.'),
          onError: (e) => notifyError(e, 'O implemento não foi excluído'),
        }),
    });

  const columns: Column<ImplementListItem>[] = [
    { key: 'plate', header: 'Placa', sortKey: 'licensePlate', render: (i) => <Text fw={600} ff="monospace">{formatPlate(i.licensePlate)}</Text> },
    { key: 'type', header: 'Tipo', sortKey: 'type', render: (i) => IMPLEMENT_TYPE[i.type].label },
    {
      key: 'model',
      header: 'Modelo',
      sortKey: 'model',
      render: (i) => (
        <div>
          <Text size="sm" fw={500}>{i.model}</Text>
          <Text size="xs" c="dimmed">{i.manufacturer}</Text>
        </div>
      ),
    },
    { key: 'year', header: 'Ano', sortKey: 'modelYear', secondary: true, render: (i) => i.modelYear },
    { key: 'capacity', header: 'Capacidade', align: 'right', secondary: true, render: capacity },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (i) => <StatusBadge value={i.status} map={IMPLEMENT_STATUS} /> },
  ];

  return (
    <>
      <PageHeader title="Implementos" description="Reboques, semirreboques, tanques, baús, siders e dollies." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por placa, modelo, fabricante, RENAVAM ou chassi"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(IMPLEMENT_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'type', placeholder: 'Tipo', data: toSelectData(IMPLEMENT_TYPE), value: list.filters.type, onChange: (v) => list.setFilter('type', v) },
        ]}
      />
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(i) => i.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(i) => navigate(`/implementos/${i.id}`)}
        rowActions={(i) => (
          <RowActions
            onEdit={can(PERMISSIONS.implements.update) ? () => navigate(`/implementos/${i.id}`) : undefined}
            onDelete={can(PERMISSIONS.implements.delete) ? () => handleDelete(i) : undefined}
          />
        )}
        renderCard={(i) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs">
              <Text fw={700} ff="monospace">{formatPlate(i.licensePlate)}</Text>
              <StatusBadge value={i.status} map={IMPLEMENT_STATUS} />
            </Group>
            <Text size="sm">{IMPLEMENT_TYPE[i.type].label} · {i.manufacturer} {i.model}</Text>
            <Text size="xs" c="dimmed">{i.modelYear} · {capacity(i)}</Text>
          </Stack>
        )}
        emptyState={
          <EmptyState
            icon={<IconTruckLoading size={28} />}
            title="Nenhum implemento cadastrado"
            description="Cadastre reboques e semirreboques para controlar as composições da frota."
            action={newButton}
          />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
