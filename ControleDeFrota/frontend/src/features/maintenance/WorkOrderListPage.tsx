import { Button, Group, Stack, Text } from '@mantine/core';
import { IconPlus, IconTool } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatCurrency, formatDateTime, formatPlate } from '../../lib/format';
import { workOrdersApi } from './api';
import { MAINTENANCE_PRIORITY, MAINTENANCE_TYPE, WORK_ORDER_STATUS, type WorkOrderListItem } from './maintenance';

const FILTERS = ['status', 'type', 'priority', 'activeOnly'] as const;

export function WorkOrderListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'openedAt', direction: 'Desc' });
  const query = workOrdersApi.useList(list.apiParams);
  const canViewCosts = can(PERMISSIONS.maintenance.viewcosts);

  const newButton = can(PERMISSIONS.maintenance.manageworkorders) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/ordens-servico/novo')}>Nova ordem de serviço</Button>
  );

  const columns: Column<WorkOrderListItem>[] = [
    { key: 'number', header: 'OS', sortKey: 'openedAt', render: (w) => <Text size="sm" fw={700} ff="monospace">{w.number}</Text> },
    {
      key: 'what', header: 'Veículo / problema', render: (w) => (
        <div style={{ maxWidth: 380 }}>
          <Text size="sm" fw={600} ff="monospace">{formatPlate(w.licensePlate)}</Text>
          <Text size="xs" c="dimmed" lineClamp={1}>{w.description}</Text>
        </div>
      ),
    },
    { key: 'type', header: 'Tipo', secondary: true, render: (w) => MAINTENANCE_TYPE[w.type].label },
    { key: 'priority', header: 'Prioridade', sortKey: 'priority', render: (w) => <StatusBadge value={w.priority} map={MAINTENANCE_PRIORITY} /> },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (w) => <StatusBadge value={w.status} map={WORK_ORDER_STATUS} /> },
    ...(canViewCosts ? [{
      key: 'cost', header: 'Custo', align: 'right' as const, secondary: true,
      render: (w: WorkOrderListItem) => formatCurrency(w.totalCost),
    }] : []),
    { key: 'opened', header: 'Aberta em', sortKey: 'openedAt', secondary: true, render: (w) => <Text size="sm">{formatDateTime(w.openedAt)}</Text> },
  ];

  return (
    <>
      <PageHeader title="Ordens de serviço" description="A fila de manutenção: solicitações aprovadas e ordens abertas diretamente." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar na descrição ou placa"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(WORK_ORDER_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'priority', placeholder: 'Prioridade', data: toSelectData(MAINTENANCE_PRIORITY), value: list.filters.priority, onChange: (v) => list.setFilter('priority', v) },
          { key: 'type', placeholder: 'Tipo', data: toSelectData(MAINTENANCE_TYPE), value: list.filters.type, onChange: (v) => list.setFilter('type', v) },
        ]}
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
        onRowClick={(w) => navigate(`/ordens-servico/${w.id}`)}
        renderCard={(w) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={700} size="sm" ff="monospace">{w.number}</Text>
              <StatusBadge value={w.status} map={WORK_ORDER_STATUS} />
            </Group>
            <Text size="sm" ff="monospace">{formatPlate(w.licensePlate)}</Text>
            <Text size="sm" lineClamp={2}>{w.description}</Text>
            <Group gap="xs">
              <StatusBadge value={w.priority} map={MAINTENANCE_PRIORITY} />
              <Text size="xs" c="dimmed">{formatDateTime(w.openedAt)}</Text>
            </Group>
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconTool size={28} />} title="Nenhuma ordem de serviço" description="Ordens de serviço aparecem aqui quando criadas diretamente ou a partir de uma solicitação aprovada." action={newButton} />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
