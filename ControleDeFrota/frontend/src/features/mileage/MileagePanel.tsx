import { useState } from 'react';
import { Alert, Button, Checkbox, Group, Modal, NumberInput, Pagination, SimpleGrid, Skeleton, Stack, Table, Text, Textarea } from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { IconAlertTriangle, IconCheck, IconGauge, IconX } from '@tabler/icons-react';
import { toApiError } from '../../api/errors';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { HeaderFact, Section } from '../../components/DetailLayout';
import { notifyError, notifySuccess } from '../../components/notify';
import { ErrorState } from '../../components/States';
import { formatDateTime, formatNumber } from '../../lib/format';
import { odometerHint } from '../../lib/mileage';
import { mileageApi, type OdometerReading } from '../operations/api';
import { ODOMETER_SOURCE, ODOMETER_STATUS } from '../operations/labels';
import type { Vehicle } from '../vehicles/vehicles';

/** Vehicle hub → "Quilometragem": current reading, record/correct, review suspicious readings and the history. */
export function MileagePanel({ vehicle }: { vehicle: Vehicle }) {
  const { can } = useAuth();
  const canRecord = can(PERMISSIONS.mileage.record) || can(PERMISSIONS.mileage.manage);
  const canManage = can(PERMISSIONS.mileage.manage);
  const [page, setPage] = useState(1);
  const [recording, setRecording] = useState(false);
  const [reviewing, setReviewing] = useState<OdometerReading | null>(null);
  const readings = mileageApi.useReadings(vehicle.id, { page, pageSize: 15 });
  const pending = mileageApi.useReadings(vehicle.id, { status: 'PendingReview', pageSize: 10 });

  return (
    <Stack gap="md">
      <Section
        title="Hodômetro"
        action={canRecord && vehicle.status !== 'Inactive' && (
          <Button variant="default" leftSection={<IconGauge size={16} />} onClick={() => setRecording(true)}>Registrar leitura</Button>
        )}
      >
        <SimpleGrid cols={{ base: 1, xs: 2 }}>
          <HeaderFact label="Leitura atual" value={`${formatNumber(vehicle.currentOdometerKm)} km`} />
          <HeaderFact label="Atualizado em" value={vehicle.odometerUpdatedAt ? formatDateTime(vehicle.odometerUpdatedAt) : 'Sem leitura registrada'} />
        </SimpleGrid>
      </Section>

      {pending.data && pending.data.totalCount > 0 && (
        <Alert color="orange" icon={<IconAlertTriangle />} title="Leituras suspeitas aguardando revisão">
          <Stack gap="xs">
            {pending.data.items.map((r) => (
              <Group key={r.id} justify="space-between" gap="xs">
                <Text size="sm"><b>{formatNumber(r.odometerKm)} km</b> em {formatDateTime(r.readAt)} — {r.anomaly}</Text>
                {canManage && <Button size="xs" variant="white" onClick={() => setReviewing(r)}>Revisar</Button>}
              </Group>
            ))}
            {!canManage && <Text size="xs">Um gestor precisa confirmar ou rejeitar. Até lá, o hodômetro atual não muda.</Text>}
          </Stack>
        </Alert>
      )}

      <Section title="Histórico de leituras" description="Nenhuma leitura é apagada: erros são rejeitados ou corrigidos, com registro de quem fez.">
        {readings.isLoading && <Skeleton height={120} />}
        {readings.error && <ErrorState error={readings.error} onRetry={() => void readings.refetch()} />}
        {readings.data && (
          <>
            <Table.ScrollContainer minWidth={600}>
              <Table verticalSpacing="xs">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Data</Table.Th>
                    <Table.Th ta="right">Hodômetro</Table.Th>
                    <Table.Th>Origem</Table.Th>
                    <Table.Th>Situação</Table.Th>
                    <Table.Th>Registrado por</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {readings.data.items.map((r) => (
                    <Table.Tr key={r.id}>
                      <Table.Td><Text size="sm">{formatDateTime(r.readAt)}</Text></Table.Td>
                      <Table.Td ta="right"><Text size="sm" fw={600}>{formatNumber(r.odometerKm)} km</Text></Table.Td>
                      <Table.Td>
                        <Text size="sm">{ODOMETER_SOURCE[r.source].label}</Text>
                        {(r.notes || r.reviewNotes) && <Text size="xs" c="dimmed">{r.reviewNotes ?? r.notes}</Text>}
                      </Table.Td>
                      <Table.Td><StatusBadge value={r.status} map={ODOMETER_STATUS} /></Table.Td>
                      <Table.Td><Text size="sm" c="dimmed">{r.recordedByName ?? 'Sistema'}</Text></Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
            {readings.data.totalPages > 1 && <Pagination size="sm" total={readings.data.totalPages} value={page} onChange={setPage} />}
          </>
        )}
      </Section>

      <RecordModal vehicle={vehicle} opened={recording} onClose={() => setRecording(false)} canCorrect={canManage} />
      <ReviewModal reading={reviewing} onClose={() => setReviewing(null)} />
    </Stack>
  );
}

function RecordModal({ vehicle, opened, onClose, canCorrect }: { vehicle: Vehicle; opened: boolean; onClose: () => void; canCorrect: boolean }) {
  const [km, setKm] = useState<number | string>('');
  const [readAt, setReadAt] = useState<Date | null>(new Date());
  const [notes, setNotes] = useState('');
  const [correction, setCorrection] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const record = mileageApi.useRecord(vehicle.id);

  const value = km === '' ? null : Number(km);
  const hint = odometerHint(value, vehicle.currentOdometerKm, vehicle.odometerUpdatedAt ? new Date(vehicle.odometerUpdatedAt) : null, readAt ?? new Date());

  const close = () => {
    setKm('');
    setNotes('');
    setCorrection(false);
    setErrors({});
    setReadAt(new Date());
    onClose();
  };

  const submit = () =>
    record.mutate({ odometerKm: value!, readAt: readAt?.toISOString() ?? null, notes: notes || null, isCorrection: correction }, {
      onSuccess: (r) => {
        if (r.status === 'PendingReview') notifySuccess('Leitura registrada como suspeita: um gestor precisa confirmá-la antes de atualizar o hodômetro.');
        else notifySuccess(correction ? 'Correção registrada.' : 'Hodômetro atualizado.');
        close();
      },
      onError: (e) => {
        setErrors(toApiError(e).fieldErrors);
        notifyError(e, 'A leitura não foi registrada');
      },
    });

  return (
    <Modal opened={opened} onClose={close} title="Registrar leitura do hodômetro" centered>
      <Stack>
        <Text size="sm" c="dimmed">Última leitura válida: <b>{formatNumber(vehicle.currentOdometerKm)} km</b>.</Text>
        <NumberInput label="Hodômetro" withAsterisk suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
          inputMode="numeric" value={km} onChange={setKm} error={errors.odometerKm} data-autofocus size="md" />
        {!correction && hint.kind === 'decrease' && (
          <Alert color="red" icon={<IconX />}>A leitura é menor que a última. Confira o valor{canCorrect ? ' ou marque como correção.' : '.'}</Alert>
        )}
        {!correction && hint.kind === 'suspicious' && (
          <Alert color="orange" icon={<IconAlertTriangle />}>
            Aumento de {formatNumber(hint.deltaKm)} km (média de {formatNumber(Math.round(hint.kmPerDay))} km/dia). Se confirmar, a leitura ficará em revisão.
          </Alert>
        )}
        {!correction && hint.kind === 'increase' && <Text size="sm" c="teal">+{formatNumber(hint.deltaKm)} km desde a última leitura.</Text>}
        <DateTimePicker label="Data da leitura" valueFormat="DD/MM/YYYY HH:mm" value={readAt} onChange={setReadAt} maxDate={new Date()} error={errors.readAt} />
        {canCorrect && (
          <Checkbox label="É uma correção (ex.: troca do painel ou leitura anterior errada)" checked={correction}
            onChange={(e) => setCorrection(e.currentTarget.checked)} />
        )}
        <Textarea label={correction ? 'Motivo da correção' : 'Observações'} withAsterisk={correction} autosize minRows={2} maxLength={500}
          value={notes} onChange={(e) => setNotes(e.currentTarget.value)} error={errors.notes} />
        <Group justify="flex-end">
          <Button variant="default" onClick={close}>Cancelar</Button>
          <Button onClick={submit} loading={record.isPending} disabled={value === null || (correction && !notes.trim())}>
            {correction ? 'Registrar correção' : 'Registrar leitura'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}

function ReviewModal({ reading, onClose }: { reading: OdometerReading | null; onClose: () => void }) {
  const [notes, setNotes] = useState('');
  const review = mileageApi.useReview();
  const act = (approve: boolean) =>
    review.mutate({ id: reading!.id, approve, notes: notes || null }, {
      onSuccess: () => {
        notifySuccess(approve ? 'Leitura confirmada e aplicada ao hodômetro.' : 'Leitura rejeitada.');
        setNotes('');
        onClose();
      },
      onError: (e) => notifyError(e, 'A revisão não foi registrada'),
    });

  return (
    <Modal opened={!!reading} onClose={onClose} title="Revisar leitura suspeita" centered>
      {reading && (
        <Stack>
          <Text size="sm"><b>{formatNumber(reading.odometerKm)} km</b> em {formatDateTime(reading.readAt)}, por {reading.recordedByName ?? 'Sistema'}.</Text>
          <Alert color="orange" icon={<IconAlertTriangle />}>{reading.anomaly}</Alert>
          <Textarea label="Justificativa" description="Obrigatória para rejeitar." autosize minRows={2} maxLength={500} value={notes}
            onChange={(e) => setNotes(e.currentTarget.value)} />
          <Group justify="flex-end">
            <Button variant="default" color="red" leftSection={<IconX size={16} />} onClick={() => act(false)} disabled={!notes.trim()} loading={review.isPending}>
              Rejeitar
            </Button>
            <Button leftSection={<IconCheck size={16} />} onClick={() => act(true)} loading={review.isPending}>Confirmar leitura</Button>
          </Group>
        </Stack>
      )}
    </Modal>
  );
}
