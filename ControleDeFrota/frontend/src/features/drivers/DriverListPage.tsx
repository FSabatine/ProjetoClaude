import { Button, Group, Stack, Text } from '@mantine/core';
import { IconPlus, IconSteeringWheel } from '@tabler/icons-react';
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
import { formatCpf, formatDate, formatPhone, formatPlate } from '../../lib/format';
import { DRIVER_ASSIGNMENT_FILTER, DRIVER_STATUS, LICENSE_ALERT_FILTER, LICENSE_CATEGORIES, LICENSE_STATE, driversApi, type DriverListItem } from './drivers';

const FILTERS = ['status', 'licenseAlert', 'licenseCategory', 'assignment'] as const;

export function DriverListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'fullName', direction: 'Asc' });
  const query = driversApi.useList(list.apiParams);
  const remove = driversApi.useRemove();

  const newButton = can(PERMISSIONS.drivers.create) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/motoristas/novo')}>
      Novo motorista
    </Button>
  );

  const handleDelete = (d: DriverListItem) =>
    confirmDelete({
      entity: 'motorista',
      name: d.fullName,
      onConfirm: () =>
        remove.mutate(d.id, {
          onSuccess: () => notifySuccess('Motorista excluído.'),
          onError: (e) => notifyError(e, 'O motorista não foi excluído'),
        }),
    });

  const license = (d: DriverListItem) => (
    <Group gap={6} wrap="nowrap">
      <Text size="sm">{formatDate(d.licenseExpiresOn)}</Text>
      {d.licenseState !== 'Valid' && <StatusBadge value={d.licenseState} map={LICENSE_STATE} />}
    </Group>
  );

  const columns: Column<DriverListItem>[] = [
    { key: 'name', header: 'Nome', sortKey: 'fullName', render: (d) => <Text size="sm" fw={600}>{d.fullName}</Text> },
    { key: 'cpf', header: 'CPF', sortKey: 'cpf', secondary: true, render: (d) => <Text size="sm" ff="monospace">{formatCpf(d.cpf)}</Text> },
    { key: 'phone', header: 'Telefone', secondary: true, render: (d) => formatPhone(d.phone) },
    { key: 'category', header: 'Categoria', sortKey: 'licenseCategory', render: (d) => d.licenseCategory },
    { key: 'license', header: 'Validade da CNH', sortKey: 'licenseExpiresOn', render: license },
    { key: 'vehicle', header: 'Veículo', render: (d) => <Text size="sm" ff="monospace" c={d.currentVehiclePlate ? undefined : 'dimmed'}>{d.currentVehiclePlate ? formatPlate(d.currentVehiclePlate) : '—'}</Text> },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (d) => <StatusBadge value={d.status} map={DRIVER_STATUS} /> },
  ];

  return (
    <>
      <PageHeader title="Motoristas" description="Condutores da empresa e a situação da habilitação (CNH)." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por nome, CPF, CNH ou placa do veículo"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(DRIVER_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'licenseAlert', placeholder: 'Alerta de CNH', data: toSelectData(LICENSE_ALERT_FILTER), value: list.filters.licenseAlert, onChange: (v) => list.setFilter('licenseAlert', v) },
          { key: 'licenseCategory', placeholder: 'Categoria', data: LICENSE_CATEGORIES.map((c) => ({ value: c, label: `Categoria ${c}` })), value: list.filters.licenseCategory, onChange: (v) => list.setFilter('licenseCategory', v) },
          { key: 'assignment', placeholder: 'Veículo', data: toSelectData(DRIVER_ASSIGNMENT_FILTER), value: list.filters.assignment, onChange: (v) => list.setFilter('assignment', v) },
        ]}
      />
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
        onRowClick={(d) => navigate(`/motoristas/${d.id}`)}
        rowActions={(d) => (
          <RowActions
            onEdit={can(PERMISSIONS.drivers.update) ? () => navigate(`/motoristas/${d.id}/editar`) : undefined}
            onDelete={can(PERMISSIONS.drivers.delete) ? () => handleDelete(d) : undefined}
          />
        )}
        renderCard={(d) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={700} truncate>{d.fullName}</Text>
              <StatusBadge value={d.status} map={DRIVER_STATUS} />
            </Group>
            <Text size="sm" c="dimmed">CNH {d.licenseCategory} · {formatPhone(d.phone)}{d.currentVehiclePlate ? ` · ${formatPlate(d.currentVehiclePlate)}` : ''}</Text>
            {license(d)}
          </Stack>
        )}
        emptyState={
          <EmptyState
            icon={<IconSteeringWheel size={28} />}
            title="Nenhum motorista cadastrado"
            description="Cadastre os motoristas para acompanhar a validade das CNHs e, nas próximas fases, as viagens."
            action={newButton}
          />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
