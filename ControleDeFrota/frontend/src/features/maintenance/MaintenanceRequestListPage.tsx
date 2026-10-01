import { useState } from 'react';
import { Button, Group, Modal, Stack, Text, Textarea } from '@mantine/core';
import { IconClipboardList, IconPlus } from '@tabler/icons-react';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatDateTime, formatPlate } from '../../lib/format';
import { maintenanceRequestsApi } from './api';
import { MAINTENANCE_PRIORITY, MAINTENANCE_REQUEST_SOURCE, MAINTENANCE_REQUEST_STATUS, type MaintenanceRequest } from './maintenance';
import { MaintenanceRequestFormModal } from './MaintenanceRequestFormModal';
import { notifyError, notifySuccess } from '../../components/notify';

const FILTERS = ['status', 'openOnly'] as const;

export function MaintenanceRequestListPage() {
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'reportedAt', direction: 'Desc' });
  const query = maintenanceRequestsApi.useList(list.apiParams);
  const approve = maintenanceRequestsApi.useApprove();
  const [creating, setCreating] = useState(false);
  const [rejecting, setRejecting] = useState<MaintenanceRequest | null>(null);

  const canManage = can(PERMISSIONS.maintenance.manageworkorders);
  const newButton = can(PERMISSIONS.maintenance.createrequest) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => setCreating(true)}>Solicitar manutenção</Button>
  );

  const handleApprove = (r: MaintenanceRequest) =>
    approve.mutate(r.id, {
      onSuccess: (saved) => notifySuccess(`Solicitação aprovada — ordem de serviço ${saved.workOrderNumber} aberta.`),
      onError: (e) => notifyError(e, 'A solicitação não foi aprovada'),
    });

  const columns: Column<MaintenanceRequest>[] = [
    { key: 'date', header: 'Data', sortKey: 'reportedAt', render: (r) => <Text size="sm">{formatDateTime(r.reportedAt)}</Text> },
    {
      key: 'what', header: 'Solicitação', render: (r) => (
        <div style={{ maxWidth: 420 }}>
          <Text size="sm" fw={600} ff="monospace">{formatPlate(r.licensePlate)}</Text>
          <Text size="xs" c="dimmed" lineClamp={1}>{r.description}</Text>
        </div>
      ),
    },
    { key: 'source', header: 'Origem', secondary: true, render: (r) => MAINTENANCE_REQUEST_SOURCE[r.source].label },
    { key: 'priority', header: 'Prioridade', sortKey: 'priority', render: (r) => <StatusBadge value={r.priority} map={MAINTENANCE_PRIORITY} /> },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (r) => <StatusBadge value={r.status} map={MAINTENANCE_REQUEST_STATUS} /> },
  ];

  return (
    <>
      <PageHeader title="Solicitações de manutenção" description="Pedidos de manutenção de motoristas, checklists, ocorrências e gestores, aguardando aprovação." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar na descrição ou placa"
        filters={[{ key: 'status', placeholder: 'Situação', data: toSelectData(MAINTENANCE_REQUEST_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) }]}
      />
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(r) => r.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        rowActions={(r) => r.status === 'Open' && canManage ? (
          <Group gap={4} wrap="nowrap">
            <Button size="xs" variant="light" loading={approve.isPending} onClick={() => handleApprove(r)}>Aprovar</Button>
            <Button size="xs" variant="light" color="red" onClick={() => setRejecting(r)}>Rejeitar</Button>
          </Group>
        ) : null}
        renderCard={(r) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={600} size="sm" ff="monospace">{formatPlate(r.licensePlate)}</Text>
              <StatusBadge value={r.status} map={MAINTENANCE_REQUEST_STATUS} />
            </Group>
            <Text size="sm" lineClamp={2}>{r.description}</Text>
            <Group gap="xs">
              <StatusBadge value={r.priority} map={MAINTENANCE_PRIORITY} />
              <Text size="xs" c="dimmed">{formatDateTime(r.reportedAt)}</Text>
            </Group>
            {r.status === 'Open' && canManage && (
              <Group gap="xs" mt={4}>
                <Button size="xs" variant="light" loading={approve.isPending} onClick={() => handleApprove(r)}>Aprovar</Button>
                <Button size="xs" variant="light" color="red" onClick={() => setRejecting(r)}>Rejeitar</Button>
              </Group>
            )}
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconClipboardList size={28} />} title="Nenhuma solicitação registrada"
            description="Solicitações de manutenção aprovadas viram ordens de serviço automaticamente." action={newButton} />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
      <MaintenanceRequestFormModal opened={creating} onClose={() => setCreating(false)} />
      <RejectModal request={rejecting} onClose={() => setRejecting(null)} />
    </>
  );
}

function RejectModal({ request, onClose }: { request: MaintenanceRequest | null; onClose: () => void }) {
  const [reason, setReason] = useState('');
  const reject = maintenanceRequestsApi.useReject();

  const submit = () =>
    reject.mutate({ id: request!.id, reason }, {
      onSuccess: () => { notifySuccess('Solicitação rejeitada.'); setReason(''); onClose(); },
      onError: (e) => notifyError(e, 'A solicitação não foi rejeitada'),
    });

  return (
    <Modal opened={!!request} onClose={onClose} title="Rejeitar solicitação" centered>
      <Stack>
        <Text size="sm">Veículo {request && formatPlate(request.licensePlate)}: {request?.description}</Text>
        <Textarea label="Motivo da rejeição" withAsterisk autosize minRows={3} maxLength={1000} value={reason}
          onChange={(e) => setReason(e.currentTarget.value)} data-autofocus />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Voltar</Button>
          <Button color="red" onClick={submit} loading={reject.isPending} disabled={!reason.trim()}>Rejeitar solicitação</Button>
        </Group>
      </Stack>
    </Modal>
  );
}
