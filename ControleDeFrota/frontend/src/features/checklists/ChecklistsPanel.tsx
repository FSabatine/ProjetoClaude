import { useState } from 'react';
import { Button, Group, Pagination, Paper, Skeleton, Stack, Text } from '@mantine/core';
import { IconChecklist, IconPlayerPlay } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime, formatPlate } from '../../lib/format';
import { checklistsApi } from '../operations/api';
import { CHECKLIST_RESULT } from '../operations/labels';

/** Checklists of a vehicle or driver (hub tab), with the shortcut to start one for this vehicle. */
export function ChecklistsPanel({ filter, vehicleId }: { filter: { vehicleId?: string; driverId?: string }; vehicleId?: string }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const [page, setPage] = useState(1);
  const query = checklistsApi.useList({ ...filter, page, pageSize: 10 }, can(PERMISSIONS.checklists.view));

  return (
    <Stack gap="md">
      {vehicleId && can(PERMISSIONS.checklists.execute) && (
        <Group justify="flex-end">
          <Button leftSection={<IconPlayerPlay size={16} />} onClick={() => navigate(`/checklists/realizar?veiculo=${vehicleId}`)}>Iniciar checklist</Button>
        </Group>
      )}
      {query.isLoading && <Skeleton height={120} />}
      {query.error && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.totalCount === 0 && (
        <Paper><EmptyState icon={<IconChecklist size={28} />} title="Nenhum checklist realizado" description="As inspeções realizadas aparecerão aqui." /></Paper>
      )}
      <Stack gap="xs">
        {query.data?.items.map((c) => (
          <Paper key={c.id} component="button" type="button" p="sm" onClick={() => navigate(`/checklists/${c.id}`)} style={{ display: 'block', width: '100%', textAlign: 'left', cursor: 'pointer' }}>
            <Group justify="space-between" wrap="nowrap" gap="xs">
              <div style={{ minWidth: 0 }}>
                <Text size="sm" fw={600}>{c.templateName}{!vehicleId && <Text span ff="monospace"> · {formatPlate(c.licensePlate)}</Text>}</Text>
                <Text size="xs" c="dimmed">{formatDateTime(c.performedAt)} · {c.performedByName ?? 'Sistema'}{c.driverName && vehicleId ? ` · ${c.driverName}` : ''}</Text>
              </div>
              <StatusBadge value={c.result} map={CHECKLIST_RESULT} />
            </Group>
          </Paper>
        ))}
      </Stack>
      {query.data && query.data.totalPages > 1 && <Pagination size="sm" total={query.data.totalPages} value={page} onChange={setPage} />}
    </Stack>
  );
}
