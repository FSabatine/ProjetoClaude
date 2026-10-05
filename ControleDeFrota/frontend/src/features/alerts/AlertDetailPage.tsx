import { useEffect, useState } from 'react';
import { Alert, Button, Group, Modal, Paper, Skeleton, Stack, Text, Textarea } from '@mantine/core';
import { IconArrowRight, IconBulb, IconCheck, IconChartBar, IconHandStop, IconX } from '@tabler/icons-react';
import { Link, useParams } from 'react-router-dom';
import { InfoGrid, Section } from '../../components/DetailLayout';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { ErrorState } from '../../components/States';
import { formatDateTime, formatPlate } from '../../lib/format';
import { alertsApi } from './api';
import { ALERT_CATEGORY, ALERT_STATUS, TRIGGER_LABEL, alertEntityLink, type AlertStatus, type FleetAlert } from './alerts';
import { AlertStatusBadge, SeverityBadge } from './components';

const ACTION: Partial<Record<AlertStatus, { label: string; icon: typeof IconCheck; color?: string; variant?: string }>> = {
  InProgress: { label: 'Assumir', icon: IconHandStop },
  Resolved: { label: 'Marcar como resolvido', icon: IconCheck, color: 'teal' },
  Dismissed: { label: 'Descartar', icon: IconX, color: 'gray', variant: 'default' },
};

export function AlertDetailPage() {
  const { id } = useParams();
  const query = alertsApi.useDetail(id);
  const markRead = alertsApi.useMarkRead();
  const [closing, setClosing] = useState<AlertStatus | null>(null);
  const a = query.data;

  // Opening the alert = the user saw it (New → Read). Never changes any other status.
  useEffect(() => {
    if (a?.status === 'New') markRead.mutate(a.id);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- once per opened alert
  }, [a?.id, a?.status]);

  const breadcrumbs = [{ label: 'Alertas', to: '/alertas' }, { label: 'Alerta' }];
  if (query.error && !a) return <><PageHeader title="Alerta" breadcrumbs={breadcrumbs} /><Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper></>;
  if (!a) return <Stack><Skeleton height={60} /><Skeleton height={220} /></Stack>;

  const target = alertEntityLink(a);
  const actions = a.nextStatuses.filter((s) => s !== 'Read');
  return (
    <>
      <PageHeader title={a.title} breadcrumbs={breadcrumbs}
        description={`${ALERT_CATEGORY[a.category].label} · ${a.trigger === 'OperationalEvent' ? a.ruleName ?? 'Regra' : TRIGGER_LABEL[a.trigger].label}`}
        action={target && <Button component={Link} to={target} rightSection={<IconArrowRight size={16} />}>Abrir o registro</Button>} />
      <Stack gap="lg">
        <Paper p={{ base: 'md', sm: 'lg' }}>
          <Group gap="xs" mb="md">
            <SeverityBadge value={a.severity} />
            <AlertStatusBadge value={a.status} />
            {a.licensePlate && <Text size="sm" fw={600}>{formatPlate(a.licensePlate)}</Text>}
          </Group>
          <Stack gap="md">
            <div>
              <Text size="xs" c="dimmed" fw={600} tt="uppercase">O que aconteceu</Text>
              <Text>{a.explanation}</Text>
            </div>
            <Alert variant="light" color="blue" icon={<IconChartBar size={18} />} title="Base do alerta">
              {a.evidence}
            </Alert>
            <Alert variant="light" color="teal" icon={<IconBulb size={18} />} title="Sugestão">
              {a.recommendedAction}
              <Text size="xs" c="dimmed" mt={4}>É uma sugestão do sistema a partir das regras configuradas — a decisão é sua.</Text>
            </Alert>
          </Stack>
          {actions.length > 0 && (
            <Group mt="lg" gap="sm">
              {actions.map((s) => {
                const action = ACTION[s]!;
                return (
                  <Button key={s} color={action.color} variant={action.variant ?? 'filled'} leftSection={<action.icon size={16} />}
                    onClick={() => setClosing(s)}>{action.label}</Button>
                );
              })}
            </Group>
          )}
        </Paper>
        <Section title="Histórico do alerta">
          <InfoGrid items={[
            { label: 'Detectado em', value: formatDateTime(a.detectedAt) },
            { label: 'Confirmado pela última vez', value: `${formatDateTime(a.lastDetectedAt)} (${a.detectionCount} verificação(ões))` },
            { label: 'Já aconteceu antes', value: a.recurrenceCount > 0 ? `Sim, ${a.recurrenceCount} vez(es) — foi resolvido e voltou` : 'Não' },
            { label: 'Regra', value: a.ruleName },
            { label: 'Assumido por', value: a.assignedToName },
            { label: 'Encerrado', value: a.closedAt ? `${formatDateTime(a.closedAt)}${a.closedByName ? ` por ${a.closedByName}` : ''}` : null },
            ...(a.closingNotes ? [{ label: 'Observação do encerramento', value: a.closingNotes }] : []),
          ]} />
        </Section>
      </Stack>
      {closing && <StatusModal alert={a} status={closing} onClose={() => setClosing(null)} />}
    </>
  );
}

function StatusModal({ alert, status, onClose }: { alert: FleetAlert; status: AlertStatus; onClose: () => void }) {
  const mutation = alertsApi.useChangeStatus(alert.id);
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const requiresNotes = status === 'Dismissed';
  const asksNotes = status !== 'InProgress';

  const submit = () => {
    if (requiresNotes && !notes.trim()) {
      setError('Motivo: informe por que o alerta está sendo descartado.');
      return;
    }
    mutation.mutate({ status, notes: notes.trim() || null }, {
      onSuccess: () => { notifySuccess(`Alerta ${ALERT_STATUS[status].label.toLowerCase()}.`); onClose(); },
      onError: (e) => { const err = notifyError(e, 'A situação do alerta não foi alterada'); setError(err.fieldErrors.notes ?? null); },
    });
  };

  return (
    <Modal opened onClose={onClose} centered title={ACTION[status]?.label ?? ALERT_STATUS[status].label}>
      <Stack gap="md">
        {status === 'InProgress' && <Text size="sm">O alerta ficará com você como responsável e sairá da lista de novos.</Text>}
        {status === 'Dismissed' && (
          <Text size="sm">Descartar esconde o alerta mesmo que a situação continue. O sistema não volta a avisar sobre ela pelos próximos 30 dias.</Text>
        )}
        {status === 'Resolved' && (
          <Text size="sm">Se a situação continuar na próxima verificação, um novo alerta será aberto e contado como recorrente.</Text>
        )}
        {asksNotes && (
          <Textarea label={requiresNotes ? 'Motivo' : 'Observação'} withAsterisk={requiresNotes} autosize minRows={2} maxLength={500}
            value={notes} onChange={(e) => { setNotes(e.currentTarget.value); setError(null); }} error={error} />
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>Cancelar</Button>
          <Button onClick={submit} loading={mutation.isPending}>Confirmar</Button>
        </Group>
      </Stack>
    </Modal>
  );
}
