import { useState } from 'react';
import { Anchor, Button, Group, Modal, Paper, Stack, Text, Textarea } from '@mantine/core';
import { IconPencil } from '@tabler/icons-react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { StatusBadge } from '../../components/common';
import { InfoGrid, Section } from '../../components/DetailLayout';
import { EntityFormPage } from '../../components/EntityFormPage';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime, formatPlate } from '../../lib/format';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { MaintenanceRequestFormModal } from '../maintenance/MaintenanceRequestFormModal';
import { occurrencesApi, type Occurrence } from '../operations/api';
import { OCCURRENCE_ACTION, OCCURRENCE_SEVERITY, OCCURRENCE_STATUS, OCCURRENCE_TYPE, type OccurrenceStatus } from '../operations/labels';
import { OccurrenceFormModal } from './OccurrenceFormModal';

export function OccurrenceDetailPage() {
  const { id } = useParams();
  const detail = occurrencesApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(o) => o && <OccurrenceDetail occurrence={o} />}</EntityFormPage>;
}

function OccurrenceDetail({ occurrence: o }: { occurrence: Occurrence }) {
  const { can } = useAuth();
  const canManage = can(PERMISSIONS.occurrences.manage);
  const [editing, setEditing] = useState(false);
  const [target, setTarget] = useState<OccurrenceStatus | null>(null);
  const [requestingMaintenance, setRequestingMaintenance] = useState(false);
  const save = occurrencesApi.useSave(o.id);

  const addPhoto = (fileId: string) =>
    save.mutate({
      vehicleId: o.vehicleId, driverId: o.driverId, implementId: o.implementId, type: o.type, severity: o.severity,
      occurredAt: o.occurredAt, location: o.location, description: o.description, fileIds: [fileId],
    }, { onSuccess: () => notifySuccess('Foto anexada.'), onError: (e) => notifyError(e, 'A foto não foi anexada') });

  // The workflow comes from the API (nextStatuses): the UI never decides which transitions exist.
  const actions = canManage && o.nextStatuses.length > 0 && (
    <Group gap="xs">
      {o.nextStatuses.map((s) => (
        <Button key={s} variant={s === 'Resolved' ? 'filled' : 'default'} color={s === 'Cancelled' ? 'red' : undefined} onClick={() => setTarget(s)}>
          {OCCURRENCE_ACTION[s]}
        </Button>
      ))}
    </Group>
  );

  return (
    <>
      <PageHeader
        title={OCCURRENCE_TYPE[o.type].label}
        description={`Registrada em ${formatDateTime(o.createdAt)} por ${o.createdByName ?? 'Sistema'}${o.source === 'Checklist' ? ' a partir de um checklist' : ''}.`}
        breadcrumbs={[{ label: 'Ocorrências', to: '/ocorrencias' }, { label: 'Detalhe' }]}
        action={<AuditHistoryButton entity="Occurrence" id={o.id} />}
      />
      <Stack gap="md">
        <Paper p="md">
          <Group justify="space-between" gap="sm">
            <Group gap="xs">
              <StatusBadge value={o.status} map={OCCURRENCE_STATUS} />
              <StatusBadge value={o.severity} map={OCCURRENCE_SEVERITY} />
            </Group>
            <Group gap="xs">
              {o.vehicleId && can(PERMISSIONS.maintenance.createrequest) && (
                <Button variant="default" onClick={() => setRequestingMaintenance(true)}>Abrir solicitação de manutenção</Button>
              )}
              {actions}
            </Group>
          </Group>
        </Paper>

        <Section
          title="O que aconteceu"
          action={canManage && o.nextStatuses.length > 0 && (
            <Button variant="subtle" leftSection={<IconPencil size={16} />} onClick={() => setEditing(true)}>Editar</Button>
          )}
        >
          <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>{o.description}</Text>
          <InfoGrid items={[
            { label: 'Quando', value: formatDateTime(o.occurredAt) },
            { label: 'Local', value: o.location },
            { label: 'Veículo', value: o.vehicleId && <Anchor component={Link} to={`/veiculos/${o.vehicleId}?aba=ocorrencias`} ff="monospace" size="sm">{formatPlate(o.licensePlate!)}</Anchor> },
            { label: 'Motorista', value: o.driverId && (can(PERMISSIONS.drivers.view)
              ? <Anchor component={Link} to={`/motoristas/${o.driverId}`} size="sm">{o.driverName}</Anchor> : o.driverName) },
            { label: 'Implemento', value: o.implementPlate && formatPlate(o.implementPlate) },
            { label: 'Checklist de origem', value: o.checklistExecutionId && can(PERMISSIONS.checklists.view) &&
              <Anchor component={Link} to={`/checklists/${o.checklistExecutionId}`} size="sm">Ver checklist</Anchor> },
          ]} />
        </Section>

        <Section title="Fotos e arquivos" action={canManage && !['Resolved', 'Cancelled'].includes(o.status) && <UploadButton camera onUploaded={(f) => addPhoto(f.id)} label="Adicionar foto" />}>
          {o.files.length === 0 ? <Text size="sm" c="dimmed">Nenhum arquivo anexado.</Text> : <AttachmentList files={o.files} />}
        </Section>

        {o.resolution && (
          <Section title={o.status === 'Cancelled' ? 'Motivo do cancelamento' : 'Resolução'}>
            <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>{o.resolution}</Text>
            <Text size="xs" c="dimmed">{formatDateTime(o.closedAt)} · {o.closedByName ?? 'Sistema'}</Text>
          </Section>
        )}
      </Stack>

      <StatusModal occurrence={o} target={target} onClose={() => setTarget(null)} />
      <OccurrenceFormModal opened={editing} onClose={() => setEditing(false)} occurrence={o} />
      {o.vehicleId && (
        <MaintenanceRequestFormModal opened={requestingMaintenance} onClose={() => setRequestingMaintenance(false)}
          defaults={{ vehicleId: o.vehicleId, vehicleLabel: o.licensePlate ? formatPlate(o.licensePlate) : null, occurrenceId: o.id }} />
      )}
    </>
  );
}

function StatusModal({ occurrence, target, onClose }: { occurrence: Occurrence; target: OccurrenceStatus | null; onClose: () => void }) {
  const [text, setText] = useState('');
  const change = occurrencesApi.useChangeStatus(occurrence.id);
  const closing = target === 'Resolved' || target === 'Cancelled';

  const submit = () =>
    change.mutate({ status: target!, resolution: text || null }, {
      onSuccess: () => { notifySuccess(`Ocorrência ${OCCURRENCE_STATUS[target!].label.toLowerCase()}.`); setText(''); onClose(); },
      onError: (e) => notifyError(e, 'A situação não foi alterada'),
    });

  return (
    <Modal opened={!!target} onClose={onClose} title={target ? OCCURRENCE_ACTION[target] : ''} centered>
      <Stack>
        {closing ? (
          <>
            <Textarea label={target === 'Cancelled' ? 'Motivo do cancelamento' : 'Como foi resolvida'} withAsterisk autosize minRows={3} maxLength={2000}
              value={text} onChange={(e) => setText(e.currentTarget.value)} data-autofocus />
            <Text size="xs" c="dimmed">Esta ação encerra a ocorrência e não pode ser desfeita. Se o problema voltar, registre uma nova ocorrência.</Text>
          </>
        ) : (
          <Text size="sm">A ocorrência passará para "{OCCURRENCE_STATUS[target ?? 'Open'].label}".</Text>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Voltar</Button>
          <Button color={target === 'Cancelled' ? 'red' : undefined} onClick={submit} loading={change.isPending} disabled={closing && !text.trim()}>
            {target ? OCCURRENCE_ACTION[target] : ''}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
