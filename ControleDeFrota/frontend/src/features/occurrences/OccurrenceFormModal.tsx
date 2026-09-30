import { useEffect, useState } from 'react';
import { Button, Chip, Group, Modal, Select, SimpleGrid, Stack, Text, TextInput, Textarea } from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { useNavigate } from 'react-router-dom';
import { toApiError } from '../../api/errors';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { toSelectData } from '../../components/common';
import { DriverPicker, VehiclePicker } from '../../components/EntityPickers';
import { notifyError, notifySuccess } from '../../components/notify';
import { occurrencesApi, type FileInfo, type Occurrence } from '../operations/api';
import { OCCURRENCE_SEVERITY, OCCURRENCE_TYPE, type OccurrenceSeverity, type OccurrenceType } from '../operations/labels';

export interface OccurrenceDefaults {
  vehicleId?: string | null;
  vehicleLabel?: string | null;
  driverId?: string | null;
  driverLabel?: string | null;
}

/** Register (or edit, while open) an occurrence. Few required fields; photos straight from the camera. */
export function OccurrenceFormModal({ opened, onClose, defaults, occurrence }: {
  opened: boolean;
  onClose: () => void;
  defaults?: OccurrenceDefaults;
  occurrence?: Occurrence;
}) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const save = occurrencesApi.useSave(occurrence?.id);
  const [vehicleId, setVehicleId] = useState<string | null>(null);
  const [driverId, setDriverId] = useState<string | null>(null);
  const [type, setType] = useState<OccurrenceType | null>(null);
  const [severity, setSeverity] = useState<OccurrenceSeverity>('Medium');
  const [occurredAt, setOccurredAt] = useState<Date | null>(new Date());
  const [location, setLocation] = useState('');
  const [description, setDescription] = useState('');
  const [files, setFiles] = useState<FileInfo[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!opened) return;
    setVehicleId(occurrence?.vehicleId ?? defaults?.vehicleId ?? null);
    setDriverId(occurrence?.driverId ?? defaults?.driverId ?? null);
    setType(occurrence?.type ?? null);
    setSeverity(occurrence?.severity ?? 'Medium');
    setOccurredAt(occurrence ? new Date(occurrence.occurredAt) : new Date());
    setLocation(occurrence?.location ?? '');
    setDescription(occurrence?.description ?? '');
    setFiles([]);
    setErrors({});
  }, [opened, occurrence, defaults]);

  const submit = () =>
    save.mutate({
      vehicleId, driverId, implementId: occurrence?.implementId ?? null, type, severity,
      occurredAt: occurredAt?.toISOString() ?? null, location: location || null, description, fileIds: files.map((f) => f.id),
    }, {
      onSuccess: (saved) => {
        notifySuccess(occurrence ? 'Ocorrência atualizada.' : 'Ocorrência registrada.');
        onClose();
        if (!occurrence && can(PERMISSIONS.occurrences.view)) navigate(`/ocorrencias/${saved.id}`);
      },
      onError: (e) => { setErrors(toApiError(e).fieldErrors); notifyError(e, 'A ocorrência não foi salva'); },
    });

  return (
    <Modal opened={opened} onClose={onClose} title={occurrence ? 'Editar ocorrência' : 'Registrar ocorrência'} centered size="lg">
      <Stack>
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <VehiclePicker label="Veículo" value={vehicleId} onChange={setVehicleId} initialLabel={occurrence?.licensePlate ?? defaults?.vehicleLabel} error={errors.vehicleId} />
          <DriverPicker label="Motorista" value={driverId} onChange={setDriverId} excludeInactive={false} initialLabel={occurrence?.driverName ?? defaults?.driverLabel} error={errors.driverId} />
        </SimpleGrid>
        <Select label="Tipo" withAsterisk data={toSelectData(OCCURRENCE_TYPE)} value={type} onChange={(v) => setType(v as OccurrenceType)} error={errors.type} searchable />
        <div>
          <Text size="sm" fw={500} mb={6}>Gravidade <Text span c="red">*</Text></Text>
          <Chip.Group value={severity} onChange={(v) => setSeverity(v as OccurrenceSeverity)}>
            <Group gap="xs">
              {(Object.keys(OCCURRENCE_SEVERITY) as OccurrenceSeverity[]).map((s) => (
                <Chip key={s} value={s} color={OCCURRENCE_SEVERITY[s].color} size="md">{OCCURRENCE_SEVERITY[s].label}</Chip>
              ))}
            </Group>
          </Chip.Group>
          <Text size="xs" c="dimmed" mt={4}>Crítica = impede o uso seguro do veículo.</Text>
        </div>
        <Textarea label="O que aconteceu" withAsterisk autosize minRows={3} maxLength={2000} value={description}
          onChange={(e) => setDescription(e.currentTarget.value)} error={errors.description} />
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <DateTimePicker label="Quando" valueFormat="DD/MM/YYYY HH:mm" value={occurredAt} onChange={setOccurredAt} maxDate={new Date()} error={errors.occurredAt} />
          <TextInput label="Local" maxLength={200} value={location} onChange={(e) => setLocation(e.currentTarget.value)} placeholder="Ex.: BR-277, km 120" />
        </SimpleGrid>
        <Stack gap="xs">
          <AttachmentList files={files} onRemove={(f) => setFiles((all) => all.filter((x) => x.id !== f.id))} />
          <Group gap="xs">
            <UploadButton camera onUploaded={(f) => setFiles((all) => [...all, f])} />
            <UploadButton onUploaded={(f) => setFiles((all) => [...all, f])} />
          </Group>
        </Stack>
        {errors[''] && <Text c="red" size="sm">{errors['']}</Text>}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose} disabled={save.isPending}>Cancelar</Button>
          <Button onClick={submit} loading={save.isPending} disabled={!type || !description.trim() || (!vehicleId && !driverId && !occurrence?.implementId)}>
            {occurrence ? 'Salvar alterações' : 'Registrar ocorrência'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
