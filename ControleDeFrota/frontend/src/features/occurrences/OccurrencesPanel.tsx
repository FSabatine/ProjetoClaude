import { useState } from 'react';
import { Button, Group, Pagination, Paper, SegmentedControl, Skeleton, Stack, Text } from '@mantine/core';
import { IconAlertTriangle, IconPlus } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime } from '../../lib/format';
import { occurrencesApi } from '../operations/api';
import { OCCURRENCE_SEVERITY, OCCURRENCE_STATUS, OCCURRENCE_TYPE } from '../operations/labels';
import { OccurrenceFormModal, type OccurrenceDefaults } from './OccurrenceFormModal';

/** Occurrences of a vehicle or driver (hub tab), open ones first by default. */
export function OccurrencesPanel({ filter, defaults }: { filter: { vehicleId?: string; driverId?: string }; defaults: OccurrenceDefaults }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const [scope, setScope] = useState<'open' | 'all'>('open');
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const query = occurrencesApi.useList({ ...filter, openOnly: scope === 'open' ? 'true' : undefined, page, pageSize: 10 });

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <SegmentedControl value={scope} onChange={(v) => { setScope(v as 'open' | 'all'); setPage(1); }}
          data={[{ value: 'open', label: 'Em aberto' }, { value: 'all', label: 'Todas' }]} />
        {can(PERMISSIONS.occurrences.create) && (
          <Button variant="default" leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>Registrar ocorrência</Button>
        )}
      </Group>
      {query.isLoading && <Skeleton height={120} />}
      {query.error && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.totalCount === 0 && (
        <Paper>
          <EmptyState icon={<IconAlertTriangle size={28} />} title={scope === 'open' ? 'Nenhuma ocorrência em aberto' : 'Nenhuma ocorrência'}
            description="Problemas, avarias e itens reprovados nos checklists aparecem aqui." />
        </Paper>
      )}
      <Stack gap="xs">
        {query.data?.items.map((o) => (
          <Paper key={o.id} component="button" type="button" p="sm" onClick={() => navigate(`/ocorrencias/${o.id}`)} style={{ display: 'block', width: '100%', textAlign: 'left', cursor: 'pointer' }}>
            <Group justify="space-between" wrap="nowrap" gap="xs" align="flex-start">
              <div style={{ minWidth: 0 }}>
                <Text size="sm" fw={600}>{OCCURRENCE_TYPE[o.type].label}</Text>
                <Text size="sm" lineClamp={2}>{o.description}</Text>
                <Text size="xs" c="dimmed">{formatDateTime(o.occurredAt)}{o.driverName ? ` · ${o.driverName}` : ''}</Text>
              </div>
              <Stack gap={4} align="flex-end">
                <StatusBadge value={o.status} map={OCCURRENCE_STATUS} />
                <StatusBadge value={o.severity} map={OCCURRENCE_SEVERITY} />
              </Stack>
            </Group>
          </Paper>
        ))}
      </Stack>
      {query.data && query.data.totalPages > 1 && <Pagination size="sm" total={query.data.totalPages} value={page} onChange={setPage} />}
      <OccurrenceFormModal opened={creating} onClose={() => setCreating(false)} defaults={defaults} />
    </Stack>
  );
}
