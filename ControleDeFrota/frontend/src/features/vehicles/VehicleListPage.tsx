import { Button, Group, Stack, Text } from '@mantine/core';
import { IconPlus, IconTruck } from '@tabler/icons-react';
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
import { VEHICLE_STATUS, VEHICLE_TYPE, vehiclesApi, type VehicleListItem } from './vehicles';

const FILTERS = ['status', 'type'] as const;

export function VehicleListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'licensePlate', direction: 'Asc' });
  const query = vehiclesApi.useList(list.apiParams);
  const remove = vehiclesApi.useRemove();

  const canCreate = can(PERMISSIONS.vehicles.create);
  const canEdit = can(PERMISSIONS.vehicles.update);
  const canDelete = can(PERMISSIONS.vehicles.delete);

  const newButton = canCreate && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/veiculos/novo')}>
      Novo veículo
    </Button>
  );

  const handleDelete = (v: VehicleListItem) =>
    confirmDelete({
      entity: 'veículo',
      name: `${formatPlate(v.licensePlate)} — ${v.manufacturer} ${v.model}`,
      onConfirm: () =>
        remove.mutate(v.id, {
          onSuccess: () => notifySuccess('Veículo excluído.'),
          onError: (e) => notifyError(e, 'O veículo não foi excluído'),
        }),
    });

  const columns: Column<VehicleListItem>[] = [
    { key: 'plate', header: 'Placa', sortKey: 'licensePlate', render: (v) => <Text fw={600} ff="monospace">{formatPlate(v.licensePlate)}</Text> },
    {
      key: 'model',
      header: 'Veículo',
      sortKey: 'model',
      render: (v) => (
        <div>
          <Text size="sm" fw={500}>{v.model}</Text>
          <Text size="xs" c="dimmed">{v.manufacturer}</Text>
        </div>
      ),
    },
    { key: 'type', header: 'Tipo', sortKey: 'type', secondary: true, render: (v) => VEHICLE_TYPE[v.type].label },
    { key: 'year', header: 'Ano', sortKey: 'modelYear', secondary: true, render: (v) => v.modelYear },
    { key: 'odometer', header: 'Hodômetro', sortKey: 'currentOdometerKm', align: 'right', render: (v) => `${formatNumber(v.currentOdometerKm)} km` },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (v) => <StatusBadge value={v.status} map={VEHICLE_STATUS} /> },
  ];

  return (
    <>
      <PageHeader title="Veículos" description="Frota de veículos da empresa: identificação, características e situação." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por placa, modelo, fabricante, RENAVAM ou chassi"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(VEHICLE_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'type', placeholder: 'Tipo', data: toSelectData(VEHICLE_TYPE), value: list.filters.type, onChange: (v) => list.setFilter('type', v) },
        ]}
      />
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(v) => v.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(v) => navigate(`/veiculos/${v.id}`)}
        rowActions={(v) => (
          <RowActions onEdit={canEdit ? () => navigate(`/veiculos/${v.id}`) : undefined} onDelete={canDelete ? () => handleDelete(v) : undefined} />
        )}
        renderCard={(v) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs">
              <Text fw={700} ff="monospace">{formatPlate(v.licensePlate)}</Text>
              <StatusBadge value={v.status} map={VEHICLE_STATUS} />
            </Group>
            <Text size="sm">{v.manufacturer} {v.model} · {v.modelYear}</Text>
            <Text size="xs" c="dimmed">{VEHICLE_TYPE[v.type].label} · {formatNumber(v.currentOdometerKm)} km</Text>
          </Stack>
        )}
        emptyState={
          <EmptyState
            icon={<IconTruck size={28} />}
            title="Nenhum veículo cadastrado"
            description="Cadastre o primeiro veículo para começar a gerenciar sua frota."
            action={newButton}
          />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
