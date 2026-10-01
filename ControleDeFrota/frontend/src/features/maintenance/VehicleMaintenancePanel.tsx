import { useState } from 'react';
import { Alert, Anchor, Badge, Group, Paper, Skeleton, Stack, Table, Tabs, Text } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { ErrorState } from '../../components/States';
import { formatDate, formatDateTime, formatNumber } from '../../lib/format';
import { vehicleMaintenanceApi } from './api';
import { MAINTENANCE_PRIORITY, MAINTENANCE_SCHEDULE_STATUS, WORK_ORDER_STATUS, type MaintenanceScheduleItem } from './maintenance';

/** Vehicle hub "Manutenção" tab (UX_UI.md): upcoming preventive items, work order history, repeated problems. */
export function VehicleMaintenancePanel({ vehicleId }: { vehicleId: string }) {
  const { can } = useAuth();
  const canCreateRequest = can(PERMISSIONS.maintenance.createrequest);
  const schedule = vehicleMaintenanceApi.useSchedule(vehicleId);
  const history = vehicleMaintenanceApi.useHistory(vehicleId, { pageSize: 10 });
  const repeated = vehicleMaintenanceApi.useRepeatedProblems(vehicleId);

  return (
    <Tabs defaultValue="proximas" keepMounted={false}>
      <Tabs.List mb="md">
        <Tabs.Tab value="proximas">Próximas</Tabs.Tab>
        <Tabs.Tab value="ordens">Ordens de serviço</Tabs.Tab>
        {!!repeated.data?.length && <Tabs.Tab value="recorrentes">Problemas recorrentes</Tabs.Tab>}
      </Tabs.List>

      <Tabs.Panel value="proximas">
        {schedule.isLoading && <Skeleton height={120} />}
        {schedule.error && <Paper><ErrorState error={schedule.error} onRetry={() => void schedule.refetch()} /></Paper>}
        {schedule.data?.length === 0 && (
          <Alert color="gray" icon={<IconInfoCircle size={18} />}>
            Nenhum plano de manutenção se aplica a este veículo ainda.
            {canCreateRequest && ' Cadastre um plano em Planos de manutenção.'}
          </Alert>
        )}
        {schedule.data && schedule.data.length > 0 && (
          <Stack gap="xs">
            {schedule.data.map((item) => <ScheduleRow key={item.maintenancePlanItemId} item={item} />)}
          </Stack>
        )}
      </Tabs.Panel>

      <Tabs.Panel value="ordens">
        {history.isLoading && <Skeleton height={120} />}
        {history.error && <Paper><ErrorState error={history.error} onRetry={() => void history.refetch()} /></Paper>}
        {history.data?.totalCount === 0 && <Text size="sm" c="dimmed">Nenhuma ordem de serviço para este veículo.</Text>}
        {history.data && history.data.totalCount > 0 && (
          <Table.ScrollContainer minWidth={480}>
            <Table verticalSpacing="xs">
              <Table.Thead><Table.Tr><Table.Th>OS</Table.Th><Table.Th>Situação</Table.Th><Table.Th>Aberta em</Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {history.data.items.map((w) => (
                  <Table.Tr key={w.id}>
                    <Table.Td><Anchor component={Link} to={`/ordens-servico/${w.id}`} size="sm" fw={600} ff="monospace">{w.number}</Anchor></Table.Td>
                    <Table.Td><StatusBadge value={w.status} map={WORK_ORDER_STATUS} /></Table.Td>
                    <Table.Td><Text size="sm">{formatDateTime(w.openedAt)}</Text></Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Tabs.Panel>

      {!!repeated.data?.length && (
        <Tabs.Panel value="recorrentes">
          <Stack gap="xs">
            {repeated.data.map((r) => (
              <Group key={r.description} justify="space-between">
                <Text size="sm">{r.description}</Text>
                <Badge color="orange">{r.occurrences}× nos últimos 90 dias</Badge>
              </Group>
            ))}
          </Stack>
        </Tabs.Panel>
      )}
    </Tabs>
  );
}

function ScheduleRow({ item }: { item: MaintenanceScheduleItem }) {
  const [expanded, setExpanded] = useState(false);
  return (
    <Paper p="sm" withBorder style={{ cursor: 'pointer' }} onClick={() => setExpanded((v) => !v)}>
      <Group justify="space-between" wrap="nowrap">
        <div>
          <Text size="sm" fw={600}>{item.serviceName}</Text>
          <Group gap={6}>
            <StatusBadge value={item.priority} map={MAINTENANCE_PRIORITY} />
            {dueText(item) && <Text size="xs" c="dimmed">{dueText(item)}</Text>}
          </Group>
        </div>
        <StatusBadge value={item.status} map={MAINTENANCE_SCHEDULE_STATUS} />
      </Group>
      {expanded && (
        <Text size="xs" c="dimmed" mt={6}>
          Última manutenção: {item.lastPerformedOn ? `${formatDate(item.lastPerformedOn)}${item.lastPerformedKm !== null ? ` · ${formatNumber(item.lastPerformedKm)} km` : ''}` : 'nunca'}.
        </Text>
      )}
    </Paper>
  );
}

function dueText(item: MaintenanceScheduleItem) {
  const parts: string[] = [];
  if (item.nextDueKm !== null) parts.push(`${formatNumber(item.nextDueKm)} km`);
  if (item.nextDueOn !== null) parts.push(formatDate(item.nextDueOn));
  if (item.nextDueHours !== null) parts.push(`${formatNumber(item.nextDueHours)} h`);
  return parts.length ? `Próxima: ${parts.join(' ou ')}` : null;
}
