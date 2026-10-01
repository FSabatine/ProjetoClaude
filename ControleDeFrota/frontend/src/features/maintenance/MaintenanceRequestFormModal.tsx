import { useEffect, useState } from 'react';
import { Button, Chip, Group, Modal, Select, SimpleGrid, Stack, Text, Textarea } from '@mantine/core';
import { toApiError } from '../../api/errors';
import { toSelectData } from '../../components/common';
import { DriverPicker, VehiclePicker } from '../../components/EntityPickers';
import { notifyError, notifySuccess } from '../../components/notify';
import { maintenanceRequestsApi } from './api';
import {
  MAINTENANCE_PRIORITY, MAINTENANCE_TYPE, type MaintenancePriority, type MaintenanceType,
} from './maintenance';

export function MaintenanceRequestFormModal({ opened, onClose, defaults }: {
  opened: boolean;
  onClose: () => void;
  defaults?: { vehicleId?: string | null; vehicleLabel?: string | null; occurrenceId?: string | null };
}) {
  const create = maintenanceRequestsApi.useCreate();
  const [vehicleId, setVehicleId] = useState<string | null>(null);
  const [driverId, setDriverId] = useState<string | null>(null);
  const [maintenanceType, setMaintenanceType] = useState<MaintenanceType>('Corrective');
  const [priority, setPriority] = useState<MaintenancePriority>('Medium');
  const [description, setDescription] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!opened) return;
    setVehicleId(defaults?.vehicleId ?? null);
    setDriverId(null);
    setMaintenanceType('Corrective');
    setPriority('Medium');
    setDescription('');
    setErrors({});
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the modal (re)opens
  }, [opened]);

  const submit = () =>
    create.mutate({
      vehicleId, driverId, source: defaults?.occurrenceId ? 'Occurrence' : 'FleetManager', maintenanceType, priority,
      description, reportedAt: null, odometerKm: null, hourMeter: null, occurrenceId: defaults?.occurrenceId ?? null,
    }, {
      onSuccess: () => { notifySuccess('Solicitação de manutenção registrada.'); onClose(); },
      onError: (e) => { setErrors(toApiError(e).fieldErrors); notifyError(e, 'A solicitação não foi registrada'); },
    });

  return (
    <Modal opened={opened} onClose={onClose} title="Solicitar manutenção" centered size="lg">
      <Stack>
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <VehiclePicker label="Veículo" value={vehicleId} onChange={setVehicleId} initialLabel={defaults?.vehicleLabel} error={errors.vehicleId} />
          <DriverPicker label="Motorista (opcional)" value={driverId} onChange={setDriverId} excludeInactive={false} error={errors.driverId} />
        </SimpleGrid>
        <Select label="Tipo" withAsterisk data={toSelectData(MAINTENANCE_TYPE)} value={maintenanceType} onChange={(v) => setMaintenanceType(v as MaintenanceType)} />
        <div>
          <Text size="sm" fw={500} mb={6}>Prioridade <Text span c="red">*</Text></Text>
          <Chip.Group value={priority} onChange={(v) => setPriority(v as MaintenancePriority)}>
            <Group gap="xs">
              {(Object.keys(MAINTENANCE_PRIORITY) as MaintenancePriority[]).map((p) => (
                <Chip key={p} value={p} color={MAINTENANCE_PRIORITY[p].color} size="md">{MAINTENANCE_PRIORITY[p].label}</Chip>
              ))}
            </Group>
          </Chip.Group>
          <Text size="xs" c="dimmed" mt={4}>Crítica = o veículo não deve ser usado até ser avaliado.</Text>
        </div>
        <Textarea label="O que precisa ser verificado" withAsterisk autosize minRows={3} maxLength={2000} value={description}
          onChange={(e) => setDescription(e.currentTarget.value)} error={errors.description} data-autofocus />
        {errors[''] && <Text c="red" size="sm">{errors['']}</Text>}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose} disabled={create.isPending}>Cancelar</Button>
          <Button onClick={submit} loading={create.isPending} disabled={!vehicleId || !description.trim()}>Registrar solicitação</Button>
        </Group>
      </Stack>
    </Modal>
  );
}
