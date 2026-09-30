import { useState } from 'react';
import { Anchor, Badge, Button, Group, Modal, Paper, Skeleton, Stack, Table, Text, Textarea } from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { modals } from '@mantine/modals';
import { IconArrowsExchange, IconSteeringWheel, IconUserOff, IconUserPlus } from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import { toApiError } from '../../api/errors';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { Section } from '../../components/DetailLayout';
import { DriverPicker } from '../../components/EntityPickers';
import { notifyError, notifySuccess } from '../../components/notify';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime, formatPlate } from '../../lib/format';
import { assignmentsApi, type Assignment } from '../operations/api';
import type { Vehicle } from '../vehicles/vehicles';

const duration = (a: Assignment) => {
  const end = a.endedAt ? new Date(a.endedAt) : new Date();
  const days = Math.max(0, Math.floor((end.getTime() - new Date(a.startedAt).getTime()) / 86_400_000));
  return days === 0 ? 'menos de 1 dia' : days === 1 ? '1 dia' : `${days} dias`;
};

function HistoryTable({ rows, show }: { rows: Assignment[]; show: 'driver' | 'vehicle' }) {
  return (
    <Table.ScrollContainer minWidth={520}>
      <Table verticalSpacing="xs">
        <Table.Thead>
          <Table.Tr>
            <Table.Th>{show === 'driver' ? 'Motorista' : 'Veículo'}</Table.Th>
            <Table.Th>Início</Table.Th>
            <Table.Th>Fim</Table.Th>
            <Table.Th>Duração</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {rows.map((a) => (
            <Table.Tr key={a.id}>
              <Table.Td>
                {show === 'driver' ? (
                  <Anchor component={Link} to={`/motoristas/${a.driverId}`} size="sm">{a.driverName}</Anchor>
                ) : (
                  <Anchor component={Link} to={`/veiculos/${a.vehicleId}`} size="sm" ff="monospace">{formatPlate(a.licensePlate)}</Anchor>
                )}
                {a.endReason && <Text size="xs" c="dimmed">{a.endReason}</Text>}
              </Table.Td>
              <Table.Td><Text size="sm">{formatDateTime(a.startedAt)}</Text></Table.Td>
              <Table.Td>{a.endedAt ? <Text size="sm">{formatDateTime(a.endedAt)}</Text> : <Badge color="cyan">Atual</Badge>}</Table.Td>
              <Table.Td><Text size="sm" c="dimmed">{duration(a)}</Text></Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
    </Table.ScrollContainer>
  );
}

/** Vehicle hub → "Motorista" tab: current driver, hand-over and the full assignment history. */
export function VehicleAssignmentPanel({ vehicle }: { vehicle: Vehicle }) {
  const { can } = useAuth();
  const canManage = can(PERMISSIONS.assignments.manage);
  const history = assignmentsApi.useForVehicle(vehicle.id, { pageSize: 50 });
  const [assigning, setAssigning] = useState(false);
  const end = assignmentsApi.useEnd();
  const current = vehicle.currentAssignment;

  const confirmEnd = () =>
    current &&
    modals.openConfirmModal({
      title: 'Encerrar alocação?',
      children: <Text size="sm"><b>{current.driverName}</b> deixará de ser o motorista de {formatPlate(vehicle.licensePlate)}. O período fica registrado no histórico.</Text>,
      labels: { confirm: 'Encerrar alocação', cancel: 'Voltar' },
      confirmProps: { color: 'red' },
      onConfirm: () =>
        end.mutate({ id: current.id, endedAt: null, reason: null }, {
          onSuccess: () => notifySuccess('Alocação encerrada.'),
          onError: (e) => notifyError(e, 'A alocação não foi encerrada'),
        }),
    });

  const inactive = vehicle.status === 'Inactive';

  return (
    <Stack gap="md">
      <Section
        title="Motorista atual"
        action={canManage && !inactive && (
          <Group gap="xs">
            {current && <Button variant="subtle" color="red" leftSection={<IconUserOff size={16} />} onClick={confirmEnd} loading={end.isPending}>Encerrar</Button>}
            <Button variant="default" leftSection={current ? <IconArrowsExchange size={16} /> : <IconUserPlus size={16} />} onClick={() => setAssigning(true)}>
              {current ? 'Trocar motorista' : 'Alocar motorista'}
            </Button>
          </Group>
        )}
      >
        {current ? (
          <Group gap="sm">
            <IconSteeringWheel size={28} stroke={1.5} />
            <div>
              <Anchor component={Link} to={`/motoristas/${current.driverId}`} fw={600}>{current.driverName}</Anchor>
              <Text size="sm" c="dimmed">Desde {formatDateTime(current.startedAt)}</Text>
            </div>
          </Group>
        ) : (
          <Text size="sm" c="dimmed">
            {inactive ? 'Veículo inativo: não pode receber motorista.' : 'Nenhum motorista alocado. O veículo está livre para uso.'}
          </Text>
        )}
      </Section>

      <Section title="Histórico de alocações" description="Todos os motoristas que já foram responsáveis por este veículo.">
        {history.isLoading && <Skeleton height={80} />}
        {history.error && <ErrorState error={history.error} onRetry={() => void history.refetch()} />}
        {history.data?.totalCount === 0 && <Text size="sm" c="dimmed">Nenhuma alocação registrada.</Text>}
        {history.data && history.data.totalCount > 0 && <HistoryTable rows={history.data.items} show="driver" />}
      </Section>

      <AssignModal vehicle={vehicle} opened={assigning} onClose={() => setAssigning(false)} />
    </Stack>
  );
}

function AssignModal({ vehicle, opened, onClose }: { vehicle: Vehicle; opened: boolean; onClose: () => void }) {
  const [driverId, setDriverId] = useState<string | null>(null);
  const [startedAt, setStartedAt] = useState<Date | null>(new Date());
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const assign = assignmentsApi.useAssign(vehicle.id);

  const close = () => {
    setDriverId(null);
    setStartedAt(new Date());
    setNotes('');
    setError(null);
    onClose();
  };

  const send = (endCurrent: boolean) =>
    assign.mutate({ driverId: driverId!, startedAt: startedAt?.toISOString() ?? null, endCurrent, notes: notes || null }, {
      onSuccess: () => {
        notifySuccess(endCurrent ? 'Troca de motorista registrada.' : 'Motorista alocado com sucesso.');
        close();
      },
      onError: (e) => {
        const apiError = toApiError(e);
        // 409 = someone would lose their current assignment: ask before doing it (never a silent hand-over).
        if (apiError.status === 409 && !endCurrent) {
          modals.openConfirmModal({
            title: 'Confirmar troca?',
            children: (
              <Stack gap="xs">
                <Text size="sm">{apiError.message}</Text>
                <Text size="sm" c="dimmed">A alocação encerrada continua no histórico.</Text>
              </Stack>
            ),
            labels: { confirm: 'Confirmar troca', cancel: 'Voltar' },
            onConfirm: () => send(true),
          });
          return;
        }
        setError(apiError.fieldErrors.startedAt ?? apiError.fieldErrors.driverId ?? null);
        notifyError(e, 'O motorista não foi alocado');
      },
    });

  return (
    <Modal opened={opened} onClose={close} title={`Motorista de ${formatPlate(vehicle.licensePlate)}`} centered>
      <Stack>
        {vehicle.currentAssignment && (
          <Paper p="sm" bg="var(--mantine-color-default-hover)">
            <Text size="sm">Motorista atual: <b>{vehicle.currentAssignment.driverName}</b>. A troca encerra essa alocação.</Text>
          </Paper>
        )}
        <DriverPicker label="Motorista" withAsterisk value={driverId} onChange={setDriverId} data-autofocus
          description="Somente motoristas ativos. A CNH vencida bloqueia a alocação." />
        <DateTimePicker label="Início" valueFormat="DD/MM/YYYY HH:mm" value={startedAt} onChange={setStartedAt} maxDate={new Date()} error={error}
          description="Agora, ou quando a entrega do veículo aconteceu." />
        <Textarea label="Observações" autosize minRows={2} maxLength={500} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />
        <Group justify="flex-end">
          <Button variant="default" onClick={close}>Cancelar</Button>
          <Button disabled={!driverId} loading={assign.isPending} onClick={() => send(false)}>Alocar motorista</Button>
        </Group>
      </Stack>
    </Modal>
  );
}

/** Driver hub → "Veículos" tab. Assignments are made from the vehicle page (the vehicle is what is being allocated). */
export function DriverAssignmentPanel({ driverId, current }: {
  driverId: string;
  current: { vehicleId: string; licensePlate: string; vehicleDescription: string; startedAt: string } | null;
}) {
  const history = assignmentsApi.useForDriver(driverId, { pageSize: 50 });
  return (
    <Stack gap="md">
      <Section title="Veículo atual">
        {current ? (
          <Group gap="sm">
            <div>
              <Anchor component={Link} to={`/veiculos/${current.vehicleId}?aba=motorista`} fw={600} ff="monospace">{formatPlate(current.licensePlate)}</Anchor>
              <Text size="sm">{current.vehicleDescription}</Text>
              <Text size="xs" c="dimmed">Desde {formatDateTime(current.startedAt)}</Text>
            </div>
          </Group>
        ) : (
          <Text size="sm" c="dimmed">Sem veículo alocado. Para alocar, abra o veículo e use "Alocar motorista".</Text>
        )}
      </Section>
      <Section title="Histórico de veículos">
        {history.isLoading && <Skeleton height={80} />}
        {history.error && <ErrorState error={history.error} onRetry={() => void history.refetch()} />}
        {history.data?.totalCount === 0 && (
          <EmptyState icon={<IconSteeringWheel size={28} />} title="Nenhuma alocação" description="Os veículos que este motorista dirigir aparecerão aqui." />
        )}
        {history.data && history.data.totalCount > 0 && <HistoryTable rows={history.data.items} show="vehicle" />}
      </Section>
    </Stack>
  );
}
