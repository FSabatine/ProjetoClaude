import { useState } from 'react';
import { Alert, Anchor, Button, Group, Modal, Paper, SimpleGrid, Stack, Table, Text, Textarea, Timeline } from '@mantine/core';
import { IconAlertTriangle, IconBan, IconCheck, IconPencil, IconReceipt } from '@tabler/icons-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { StatusBadge } from '../../components/common';
import { HeaderFact, InfoGrid, Section } from '../../components/DetailLayout';
import { EntityFormPage } from '../../components/EntityFormPage';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { formatCurrency, formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { formatQuantity, formatUnitPrice } from '../../lib/fuel';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { fuelingsApi } from './api';
import { AnomalyList, CONSUMPTION_HELP, ConsumptionDetails, InfoHint } from './components';
import { FUELING_STATUS, PAYMENT_METHOD, type Fueling } from './fuel';

export function FuelingDetailPage() {
  const { id } = useParams();
  const detail = fuelingsApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(f) => f && <FuelingView fueling={f} />}</EntityFormPage>;
}

function FuelingView({ fueling: f }: { fueling: Fueling }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const [action, setAction] = useState<'review' | 'cancel' | null>(null);
  const attach = fuelingsApi.useAttach(f.id);
  const plate = formatPlate(f.licensePlate);
  const canAttach = f.status !== 'Cancelled' && (can(PERMISSIONS.fuel.correct) || (can(PERMISSIONS.fuel.create) && f.canSeeCosts));

  const buttons = (
    <Group gap="xs">
      <AuditHistoryButton entity="Fueling" id={f.id} />
      {f.actions.canCancel && <Button variant="default" color="red" leftSection={<IconBan size={18} />} onClick={() => setAction('cancel')}>Cancelar</Button>}
      {f.actions.canCorrect && <Button variant="default" leftSection={<IconPencil size={18} />} onClick={() => navigate(`/abastecimentos/${f.id}/corrigir`)}>Corrigir</Button>}
      {f.actions.canReview && <Button leftSection={<IconCheck size={18} />} onClick={() => setAction('review')}>Revisar</Button>}
    </Group>
  );

  return (
    <>
      <PageHeader
        title={`Abastecimento · ${plate}`}
        description={`${formatDateTime(f.fueledAt)} · ${f.fuelStationName ?? 'posto não informado'}`}
        breadcrumbs={[{ label: 'Abastecimentos', to: '/abastecimentos' }, { label: `${plate} · ${formatDateTime(f.fueledAt)}` }]}
        action={buttons}
      />
      <Stack gap="md">
        {f.status === 'Cancelled' && (
          <Alert color="gray" icon={<IconBan size={18} />} title="Abastecimento cancelado">
            {f.cancellationReason} — por {f.cancelledByName ?? '—'} em {formatDateTime(f.cancelledAt)}. Ele não entra em totais nem no consumo.
            {f.odometerReadingStatus === 'Valid' && ' A leitura de hodômetro gerada por ele foi mantida; se o km estava errado, corrija na aba Quilometragem do veículo.'}
          </Alert>
        )}
        {f.status === 'PendingReview' && (
          <Alert color="orange" icon={<IconAlertTriangle size={18} />} title="Requer revisão">
            Algo neste registro foge do padrão. Confira os dados (cupom, hodômetro, quantidade) e escolha: revisar (está correto), corrigir ou cancelar.
            Um alerta não significa erro nem irregularidade.
          </Alert>
        )}

        <Paper p="md">
          <SimpleGrid cols={{ base: 2, md: 4 }} spacing="md">
            <HeaderFact label="Situação" value={<StatusBadge value={f.status} map={FUELING_STATUS} />} />
            <HeaderFact label="Quantidade" value={formatQuantity(f.quantity, f.unit)} hint={f.isFullTank ? 'tanque cheio' : 'complemento (parcial)'} />
            <HeaderFact label="Total" value={f.canSeeCosts ? formatCurrency(f.totalAmount) : '—'} hint={f.canSeeCosts ? formatUnitPrice(f.unitPrice, f.unit) : 'sem permissão para ver custos'} />
            <HeaderFact label="Hodômetro" value={`${formatNumber(f.odometerKm)} km`}
              hint={f.odometerReadingStatus === 'PendingReview' ? 'leitura em revisão' : f.odometerReadingStatus ? 'registrado no histórico' : 'lançado após leituras mais recentes'} />
          </SimpleGrid>
        </Paper>

        <AnomalyList anomalies={f.anomalies} />

        <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
          <Section title="Dados do abastecimento">
            <InfoGrid cols={2} items={[
              { label: 'Veículo', value: <Anchor component={Link} to={`/veiculos/${f.vehicleId}?aba=combustivel`}>{plate}</Anchor> },
              { label: 'Motorista', value: f.driverName },
              { label: 'Combustível', value: f.fuelTypeName },
              { label: 'Posto', value: f.fuelStationId ? <Anchor component={Link} to={`/postos/${f.fuelStationId}`}>{f.fuelStationName}</Anchor> : null },
              { label: 'Forma de pagamento', value: PAYMENT_METHOD[f.paymentMethod].label },
              { label: 'Cupom / nota', value: f.receiptNumber },
              { label: 'Registrado por', value: `${f.createdByName ?? '—'} em ${formatDateTime(f.createdAt)}` },
              { label: 'Revisão', value: f.reviewedAt ? `${f.reviewedByName ?? '—'} em ${formatDateTime(f.reviewedAt)}: ${f.reviewNotes}` : null },
            ]} />
            {f.notes && <Text size="sm" c="dimmed" style={{ whiteSpace: 'pre-wrap' }}>{f.notes}</Text>}
          </Section>

          <Section title="Consumo" action={<InfoHint label="Como o consumo é calculado">{CONSUMPTION_HELP}</InfoHint>}>
            <ConsumptionDetails result={f.consumptionResult} consumption={f.consumption} unit={f.unit} expected={f.expectedConsumption}
              baselineSource={f.baselineSource} deviation={f.consumptionDeviationPercent} distanceKm={f.segmentDistanceKm} quantity={f.segmentQuantity} />
            {f.canSeeCosts && f.segmentCost !== null && f.segmentDistanceKm ? (
              <Text size="xs" c="dimmed">Custo por km no trecho: {formatCurrency(f.segmentCost / f.segmentDistanceKm)}</Text>
            ) : null}
          </Section>
        </SimpleGrid>

        <Section title="Comprovantes" description="Cupom, nota fiscal ou foto do abastecimento."
          action={canAttach && <UploadButton camera label="Anexar foto" onUploaded={(file) => attach.mutate([file.id], {
            onSuccess: () => notifySuccess('Comprovante anexado.'), onError: (e) => notifyError(e, 'O comprovante não foi anexado'),
          })} />}>
          {f.files.length > 0 ? <AttachmentList files={f.files} /> : (
            <Group gap="xs" c="dimmed"><IconReceipt size={18} /><Text size="sm" c="dimmed">Nenhum comprovante anexado.</Text></Group>
          )}
        </Section>

        {f.corrections.length > 0 && (
          <Section title="Correções" description="O registro original é preservado: cada correção guarda quem, quando, o motivo e os valores.">
            <Timeline bulletSize={14} lineWidth={2}>
              {f.corrections.map((c) => (
                <Timeline.Item key={c.id} title={<Text size="sm" fw={600}>{c.correctedByName ?? '—'} · {formatDateTime(c.correctedAt)}</Text>}>
                  <Text size="sm" mb={4}>Motivo: {c.reason}</Text>
                  <Table.ScrollContainer minWidth={320}>
                    <Table fz="xs" withRowBorders={false}>
                      <Table.Tbody>
                        {c.changes.map((ch) => (
                          <Table.Tr key={ch.field}>
                            <Table.Td c="dimmed">{ch.label}</Table.Td>
                            <Table.Td td="line-through" c="dimmed">{ch.from ?? '—'}</Table.Td>
                            <Table.Td fw={500}>{ch.to ?? '—'}</Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  </Table.ScrollContainer>
                </Timeline.Item>
              ))}
            </Timeline>
          </Section>
        )}
      </Stack>
      <ReasonModal fueling={f} action={action} onClose={() => setAction(null)} />
    </>
  );
}

function ReasonModal({ fueling, action, onClose }: { fueling: Fueling; action: 'review' | 'cancel' | null; onClose: () => void }) {
  const review = fuelingsApi.useReview(fueling.id);
  const cancel = fuelingsApi.useCancel(fueling.id);
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const mutation = action === 'cancel' ? cancel : review;
  const mileagePending = fueling.odometerReadingStatus === 'PendingReview';

  const submit = () => {
    if (!reason.trim()) {
      setError(action === 'cancel' ? 'Informe o motivo do cancelamento.' : 'Descreva o que foi verificado.');
      return;
    }
    mutation.mutate(reason.trim(), {
      onSuccess: () => {
        notifySuccess(action === 'cancel' ? 'Abastecimento cancelado.' : 'Abastecimento revisado.');
        setReason('');
        onClose();
      },
      onError: (e) => {
        const apiError = notifyError(e, action === 'cancel' ? 'O abastecimento não foi cancelado' : 'A revisão não foi registrada');
        setError(apiError.fieldErrors.reason ?? null);
      },
    });
  };

  return (
    <Modal opened={!!action} onClose={onClose} centered title={action === 'cancel' ? 'Cancelar abastecimento?' : 'Revisar abastecimento'}>
      <Stack>
        {action === 'cancel' ? (
          <Text size="sm">O registro fica no histórico como cancelado e deixa de contar nos totais e no consumo. Esta ação não pode ser desfeita.</Text>
        ) : (
          <Text size="sm">
            Confirme que os dados estão corretos depois de conferir o comprovante. Os alertas ficam marcados como revisados, com seu nome.
            {mileagePending && ' O hodômetro informado também será confirmado e passará a valer para o veículo.'}
          </Text>
        )}
        <Textarea label={action === 'cancel' ? 'Motivo do cancelamento' : 'O que foi verificado'} withAsterisk autosize minRows={3} maxLength={1000}
          placeholder={action === 'cancel' ? 'Ex.: lançado em duplicidade' : 'Ex.: conferido com o cupom, dois tanques abastecidos'}
          value={reason} onChange={(e) => { setReason(e.currentTarget.value); setError(null); }} error={error} data-autofocus />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose} disabled={mutation.isPending}>Voltar</Button>
          <Button color={action === 'cancel' ? 'red' : undefined} loading={mutation.isPending} onClick={submit}>
            {action === 'cancel' ? 'Cancelar abastecimento' : 'Confirmar revisão'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
