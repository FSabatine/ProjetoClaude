import { Button, Checkbox, Group, NumberInput, Stack, Text } from '@mantine/core';
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
import { DriverPicker } from '../../components/EntityPickers';
import { VEHICLE_OPERATIONAL_STATUS } from '../operations/labels';
import { VEHICLE_TYPE, vehiclesApi, type VehicleListItem } from './vehicles';

const FILTERS = ['operationalStatus', 'type', 'driverId', 'minOdometerKm', 'maxOdometerKm', 'staleMileage', 'pendingMileageReview'] as const;

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
    { key: 'driver', header: 'Motorista', render: (v) => <Text size="sm" c={v.currentDriverName ? undefined : 'dimmed'}>{v.currentDriverName ?? '—'}</Text> },
    { key: 'odometer', header: 'Hodômetro', sortKey: 'currentOdometerKm', align: 'right', render: (v) => `${formatNumber(v.currentOdometerKm)} km` },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (v) => <StatusBadge value={v.operationalStatus} map={VEHICLE_OPERATIONAL_STATUS} /> },
  ];

  return (
    <>
      <PageHeader title="Veículos" description="A frota e a situação operacional de cada veículo: quem dirige, hodômetro e disponibilidade." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por placa, modelo, motorista, RENAVAM ou chassi"
        filters={[
          { key: 'operationalStatus', placeholder: 'Situação', data: toSelectData(VEHICLE_OPERATIONAL_STATUS), value: list.filters.operationalStatus, onChange: (v) => list.setFilter('operationalStatus', v) },
          { key: 'type', placeholder: 'Tipo', data: toSelectData(VEHICLE_TYPE), value: list.filters.type, onChange: (v) => list.setFilter('type', v) },
        ]}
      />
      <Group gap="sm" mb="md" mt={-8} align="flex-end" wrap="wrap">
        <DriverPicker size="xs" label="Motorista" excludeInactive={false} value={list.filters.driverId} onChange={(v) => list.setFilter('driverId', v)} w={220} />
        <NumberInput size="xs" label="Km mínimo" w={130} thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
          value={list.filters.minOdometerKm ?? ''} onChange={(v) => list.setFilter('minOdometerKm', v === '' ? null : String(v))} />
        <NumberInput size="xs" label="Km máximo" w={130} thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
          value={list.filters.maxOdometerKm ?? ''} onChange={(v) => list.setFilter('maxOdometerKm', v === '' ? null : String(v))} />
        <Checkbox size="xs" mb={6} label="Sem leitura de hodômetro há 7 dias" checked={list.filters.staleMileage === 'true'}
          onChange={(e) => list.setFilter('staleMileage', e.currentTarget.checked ? 'true' : null)} />
        <Checkbox size="xs" mb={6} label="Leitura de hodômetro aguardando revisão" checked={list.filters.pendingMileageReview === 'true'}
          onChange={(e) => list.setFilter('pendingMileageReview', e.currentTarget.checked ? 'true' : null)} />
      </Group>
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
          <RowActions onEdit={canEdit ? () => navigate(`/veiculos/${v.id}/editar`) : undefined} onDelete={canDelete ? () => handleDelete(v) : undefined} />
        )}
        renderCard={(v) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs">
              <Text fw={700} ff="monospace">{formatPlate(v.licensePlate)}</Text>
              <StatusBadge value={v.operationalStatus} map={VEHICLE_OPERATIONAL_STATUS} />
            </Group>
            <Text size="sm">{v.manufacturer} {v.model} · {v.modelYear}</Text>
            {v.currentDriverName && <Text size="xs">{v.currentDriverName}</Text>}
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
