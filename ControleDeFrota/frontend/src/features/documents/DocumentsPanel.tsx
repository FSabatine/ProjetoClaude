import { useState, type ReactNode } from 'react';
import { ActionIcon, Button, Card, Group, Menu, SimpleGrid, Skeleton, Stack, Switch, Text } from '@mantine/core';
import { IconDots, IconFileText, IconPaperclip, IconPencil, IconPlus, IconRefresh, IconTrash } from '@tabler/icons-react';
import { api } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AttachmentList } from '../../components/Attachments';
import { StatusBadge } from '../../components/common';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDate } from '../../lib/format';
import { dueLabel } from '../../lib/mileage';
import { documentsApi, type DocumentListItem, type FleetDocument } from '../operations/api';
import { DOCUMENT_STATUS, type DocumentOwnerType } from '../operations/labels';
import { DocumentFormModal, type DocumentModalMode } from './DocumentFormModal';

const OWNER_FILTER: Record<DocumentOwnerType, string> = {
  Vehicle: 'vehicleId',
  Driver: 'driverId',
  Implement: 'implementId',
  Company: 'ownerType',
};

/** Documents of one owner (vehicle, driver, implement or the company) as cards: status first, due date second. */
export function DocumentsPanel({ ownerType, ownerId, extra }: { ownerType: DocumentOwnerType; ownerId: string | null; extra?: ReactNode }) {
  const { can } = useAuth();
  const [includeReplaced, setIncludeReplaced] = useState(false);
  const [mode, setMode] = useState<DocumentModalMode | null>(null);
  const filterKey = OWNER_FILTER[ownerType];
  const query = documentsApi.useList({
    [filterKey]: ownerType === 'Company' ? 'Company' : ownerId,
    includeReplaced: includeReplaced ? 'true' : undefined,
    pageSize: 100,
  });
  const remove = documentsApi.useRemove();
  const canManage = can(PERMISSIONS.documents.manage);
  const canDelete = can(PERMISSIONS.documents.delete);

  const open = async (d: DocumentListItem, kind: 'edit' | 'renew') => {
    try {
      const document = await api.get<FleetDocument>(`/documents/${d.id}`).then((r) => r.data);
      setMode({ kind, document });
    } catch (e) {
      notifyError(e, 'Não foi possível abrir o documento');
    }
  };

  const handleDelete = (d: DocumentListItem) =>
    confirmDelete({
      entity: 'documento',
      name: `${d.documentTypeName}${d.number ? ` nº ${d.number}` : ''}`,
      onConfirm: () => remove.mutate(d.id, {
        onSuccess: () => notifySuccess('Documento excluído.'),
        onError: (e) => notifyError(e, 'O documento não foi excluído'),
      }),
    });

  const addButton = canManage && (
    <Button variant="default" leftSection={<IconPlus size={16} />} onClick={() => setMode({ kind: 'create' })}>Novo documento</Button>
  );

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Switch label="Mostrar substituídos" checked={includeReplaced} onChange={(e) => setIncludeReplaced(e.currentTarget.checked)} />
        <Group gap="xs">{extra}{addButton}</Group>
      </Group>

      {query.isLoading && <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>{[1, 2, 3].map((i) => <Skeleton key={i} height={120} />)}</SimpleGrid>}
      {query.error && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.totalCount === 0 && (
        <Card>
          <EmptyState icon={<IconFileText size={28} />} title="Nenhum documento" description="Cadastre CRLV, seguro, exames e outros documentos para receber alertas de vencimento." action={addButton} />
        </Card>
      )}
      <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
        {query.data?.items.map((d) => (
          <Card key={d.id} padding="md" style={{ opacity: d.status === 'Replaced' ? 0.65 : 1 }}>
            <Group justify="space-between" align="flex-start" wrap="nowrap" gap="xs">
              <div style={{ minWidth: 0 }}>
                <Text fw={600} size="sm">{d.documentTypeName}</Text>
                {d.number && <Text size="xs" c="dimmed">Nº {d.number}</Text>}
              </div>
              {(canManage || canDelete) && d.status !== 'Replaced' && (
                <Menu position="bottom-end" withinPortal>
                  <Menu.Target>
                    <ActionIcon variant="subtle" color="gray" aria-label="Ações do documento"><IconDots size={16} /></ActionIcon>
                  </Menu.Target>
                  <Menu.Dropdown>
                    {canManage && <Menu.Item leftSection={<IconPencil size={16} />} onClick={() => void open(d, 'edit')}>Editar</Menu.Item>}
                    {canManage && d.expiresOn && <Menu.Item leftSection={<IconRefresh size={16} />} onClick={() => void open(d, 'renew')}>Renovar</Menu.Item>}
                    {canDelete && <Menu.Item color="red" leftSection={<IconTrash size={16} />} onClick={() => handleDelete(d)}>Excluir</Menu.Item>}
                  </Menu.Dropdown>
                </Menu>
              )}
            </Group>
            <Group gap="xs" mt="sm">
              <StatusBadge value={d.status} map={DOCUMENT_STATUS} />
              {d.fileCount > 0 && <Group gap={2}><IconPaperclip size={14} /><Text size="xs">{d.fileCount}</Text></Group>}
            </Group>
            <Text size="sm" mt="xs">
              {d.expiresOn ? <>Vencimento {formatDate(d.expiresOn)} <Text span size="xs" c="dimmed">({dueLabel(d.daysUntilExpiration)})</Text></> : 'Sem vencimento'}
            </Text>
            {canManage && d.fileCount === 0 && d.status !== 'Replaced' && (
              <Button size="compact-xs" variant="subtle" mt="xs" onClick={() => void open(d, 'edit')}>Anexar arquivo</Button>
            )}
            {!canManage && d.fileCount > 0 && <ViewFiles id={d.id} />}
          </Card>
        ))}
      </SimpleGrid>

      <DocumentFormModal ownerType={ownerType} ownerId={ownerId} mode={mode} onClose={() => setMode(null)} />
    </Stack>
  );
}

/** Read-only users still see the files of a document. */
function ViewFiles({ id }: { id: string }) {
  const [opened, setOpened] = useState(false);
  const detail = documentsApi.useDetail(opened ? id : null);
  if (!opened) return <Button size="compact-xs" variant="subtle" mt="xs" onClick={() => setOpened(true)}>Ver arquivos</Button>;
  return detail.data ? <Stack mt="xs"><AttachmentList files={detail.data.files} /></Stack> : <Skeleton height={40} mt="xs" />;
}

