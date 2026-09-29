import { Button, Drawer, Skeleton, Stack, Text, Timeline } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconHistory, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ErrorState } from '../../components/States';
import { formatDateTime } from '../../lib/format';

type AuditAction = 'Created' | 'Updated' | 'Deleted';
type JsonValue = string | number | boolean | null | JsonValue[] | { [k: string]: JsonValue };

interface AuditEntry {
  id: number;
  action: AuditAction;
  occurredAt: string;
  userName: string | null;
  changes: { field: string; old: JsonValue; new: JsonValue }[];
}

const ACTION = {
  Created: { label: 'Cadastro', icon: IconPlus, color: 'teal' },
  Updated: { label: 'Alteração', icon: IconPencil, color: 'blue' },
  Deleted: { label: 'Exclusão', icon: IconTrash, color: 'red' },
} as const;

const show = (value: JsonValue) => (value === null || value === '' ? '—' : Array.isArray(value) ? value.join(', ') : String(value));

/**
 * "Who changed what, when, from → to" for one record (DATABASE.md, auditing). Field names are shown
 * as stored; a per-module label map is a planned refinement.
 */
export function AuditHistoryButton({ entity, id }: { entity: 'Vehicle' | 'Implement' | 'Driver' | 'User' | 'Company'; id: string }) {
  const { can } = useAuth();
  const [opened, { open, close }] = useDisclosure(false);
  const query = useQuery({
    queryKey: ['audit', entity, id],
    queryFn: () => api.get<AuditEntry[]>(`/audit/${entity}/${id}`).then((r) => r.data),
    enabled: opened,
  });

  if (!can(PERMISSIONS.audit.view)) return null;

  return (
    <>
      <Button variant="default" leftSection={<IconHistory size={18} />} onClick={open}>
        Histórico
      </Button>
      <Drawer opened={opened} onClose={close} title="Histórico de alterações" position="right" size="md">
        {query.isLoading && <Stack>{[1, 2, 3].map((i) => <Skeleton key={i} height={64} />)}</Stack>}
        {query.error && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
        {query.data?.length === 0 && <Text c="dimmed" size="sm">Nenhuma alteração registrada.</Text>}
        {query.data && query.data.length > 0 && (
          <Timeline bulletSize={26} lineWidth={2}>
            {query.data.map((entry) => {
              const action = ACTION[entry.action];
              return (
                <Timeline.Item key={entry.id} bullet={<action.icon size={14} />} color={action.color} title={action.label}>
                  <Text size="xs" c="dimmed">
                    {formatDateTime(entry.occurredAt)} · {entry.userName ?? 'Sistema'}
                  </Text>
                  {entry.action === 'Updated' && (
                    <Stack gap={2} mt={6}>
                      {entry.changes.map((c) => (
                        <Text key={c.field} size="xs">
                          <b>{c.field}</b>: {show(c.old)} → {show(c.new)}
                        </Text>
                      ))}
                    </Stack>
                  )}
                </Timeline.Item>
              );
            })}
          </Timeline>
        )}
      </Drawer>
    </>
  );
}
