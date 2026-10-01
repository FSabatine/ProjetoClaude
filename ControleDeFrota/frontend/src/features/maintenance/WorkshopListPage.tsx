import { useState } from 'react';
import { Button, Stack, Text } from '@mantine/core';
import { IconBuildingFactory2, IconPlus } from '@tabler/icons-react';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, RowActions, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { WORKSHOP_STATUS, workshopsApi, type Workshop } from './maintenance';
import { WorkshopFormModal } from './WorkshopFormModal';

const FILTERS = ['status'] as const;

export function WorkshopListPage() {
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'name', direction: 'Asc' });
  const query = workshopsApi.useList(list.apiParams);
  const remove = workshopsApi.useRemove();
  const [editing, setEditing] = useState<Workshop | null | undefined>(undefined);

  const newButton = can(PERMISSIONS.maintenance.manageworkshops) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing(null)}>Nova oficina</Button>
  );

  const handleDelete = (w: Workshop) =>
    confirmDelete({
      entity: 'oficina',
      name: w.name,
      onConfirm: () => remove.mutate(w.id, { onSuccess: () => notifySuccess('Oficina excluída.'), onError: (e) => notifyError(e, 'A oficina não foi excluída') }),
    });

  const columns: Column<Workshop>[] = [
    { key: 'name', header: 'Nome', sortKey: 'name', render: (w) => <Text size="sm" fw={600}>{w.name}</Text> },
    { key: 'specialties', header: 'Especialidades', secondary: true, render: (w) => <Text size="sm" c="dimmed">{w.specialties ?? '—'}</Text> },
    { key: 'phone', header: 'Telefone', secondary: true, render: (w) => w.phone ?? '—' },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (w) => <StatusBadge value={w.status} map={WORKSHOP_STATUS} /> },
  ];

  return (
    <>
      <PageHeader title="Oficinas" description="Oficinas internas e externas que executam ordens de serviço." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar pelo nome"
        filters={[{ key: 'status', placeholder: 'Situação', data: toSelectData(WORKSHOP_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) }]}
      />
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(w) => w.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={can(PERMISSIONS.maintenance.manageworkshops) ? (w) => setEditing(w) : undefined}
        rowActions={(w) => (
          <RowActions
            onEdit={can(PERMISSIONS.maintenance.manageworkshops) ? () => setEditing(w) : undefined}
            onDelete={can(PERMISSIONS.maintenance.manageworkshops) ? () => handleDelete(w) : undefined}
          />
        )}
        renderCard={(w) => (
          <Stack gap={4}>
            <Text fw={600} size="sm">{w.name}</Text>
            <Text size="xs" c="dimmed">{w.specialties ?? 'Sem especialidades cadastradas'}</Text>
            <StatusBadge value={w.status} map={WORKSHOP_STATUS} />
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconBuildingFactory2 size={28} />} title="Nenhuma oficina cadastrada"
            description="Cadastre as oficinas internas e externas que vão executar as ordens de serviço." action={newButton} />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
      <WorkshopFormModal opened={editing !== undefined} onClose={() => setEditing(undefined)} workshop={editing ?? undefined} />
    </>
  );
}
