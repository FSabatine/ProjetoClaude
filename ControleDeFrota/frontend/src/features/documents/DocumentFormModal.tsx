import { useEffect, useState } from 'react';
import { Button, Group, Modal, Select, SimpleGrid, Stack, Text, TextInput, Textarea } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { toApiError } from '../../api/errors';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { notifyError, notifySuccess } from '../../components/notify';
import { fromApiDate, toApiDate } from '../../lib/format';
import { documentsApi, documentTypesApi, filesApi, type FileInfo, type FleetDocument } from '../operations/api';
import type { DocumentOwnerType } from '../operations/labels';

export type DocumentModalMode =
  | { kind: 'create' }
  | { kind: 'edit'; document: FleetDocument }
  | { kind: 'renew'; document: FleetDocument };

/**
 * Create, edit or renew a document. Renewing creates a new document of the same type and owner and marks the old one
 * as replaced, so it leaves the alerts but stays in the history.
 */
export function DocumentFormModal({ ownerType, ownerId, mode, onClose }: {
  ownerType: DocumentOwnerType;
  ownerId: string | null;
  mode: DocumentModalMode | null;
  onClose: () => void;
}) {
  const types = documentTypesApi.useList(ownerType);
  const create = documentsApi.useCreate();
  const update = documentsApi.useUpdate();
  const removeFile = filesApi.useRemove();
  const source = mode && mode.kind !== 'create' ? mode.document : null;

  const [typeId, setTypeId] = useState<string | null>(null);
  const [number, setNumber] = useState('');
  const [issuedOn, setIssuedOn] = useState<Date | null>(null);
  const [expiresOn, setExpiresOn] = useState<Date | null>(null);
  const [notes, setNotes] = useState('');
  const [newFiles, setNewFiles] = useState<FileInfo[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!mode) return;
    const editing = mode.kind === 'edit' ? mode.document : null;
    setTypeId(source?.documentTypeId ?? null);
    setNumber(editing?.number ?? '');
    setIssuedOn(fromApiDate(editing?.issuedOn));
    setExpiresOn(fromApiDate(editing?.expiresOn));
    setNotes(editing?.notes ?? '');
    setNewFiles([]);
    setErrors({});
  }, [mode, source]);

  const type = types.data?.find((t) => t.id === typeId);
  const hasExpiration = type?.hasExpiration ?? source?.typeHasExpiration ?? true;
  const saving = create.isPending || update.isPending;

  const onError = (e: unknown) => {
    setErrors(toApiError(e).fieldErrors);
    notifyError(e, 'O documento não foi salvo');
  };

  const submit = () => {
    const fields = {
      number: number || null,
      issuedOn: toApiDate(issuedOn),
      expiresOn: hasExpiration ? toApiDate(expiresOn) : null,
      notes: notes || null,
      fileIds: newFiles.map((f) => f.id),
    };
    if (mode?.kind === 'edit') {
      update.mutate({ id: mode.document.id, ...fields }, { onSuccess: () => { notifySuccess('Documento atualizado.'); onClose(); }, onError });
    } else {
      create.mutate(
        { documentTypeId: typeId!, ownerId, ...fields, replacesDocumentId: mode?.kind === 'renew' ? mode.document.id : null },
        { onSuccess: () => { notifySuccess(mode?.kind === 'renew' ? 'Documento renovado.' : 'Documento cadastrado.'); onClose(); }, onError },
      );
    }
  };

  const title = mode?.kind === 'edit' ? 'Editar documento' : mode?.kind === 'renew' ? `Renovar ${source?.documentTypeName}` : 'Novo documento';

  return (
    <Modal opened={!!mode} onClose={onClose} title={title} centered size="lg">
      <Stack>
        {mode?.kind === 'renew' && (
          <Text size="sm" c="dimmed">O documento atual ficará como "Substituído" e sairá dos alertas. Informe os dados do novo documento.</Text>
        )}
        <Select
          label="Tipo de documento"
          withAsterisk
          searchable
          data={(types.data ?? []).map((t) => ({ value: t.id, label: t.name }))}
          value={typeId}
          onChange={setTypeId}
          disabled={mode?.kind !== 'create'}
          error={errors.documentTypeId}
          nothingFoundMessage="Nenhum tipo cadastrado. Configure em Tipos de documento."
        />
        <SimpleGrid cols={{ base: 1, sm: 3 }}>
          <TextInput label="Número" maxLength={60} value={number} onChange={(e) => setNumber(e.currentTarget.value)} error={errors.number} />
          <DateInput label="Emissão" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable value={issuedOn} onChange={setIssuedOn} maxDate={new Date()} error={errors.issuedOn} />
          {hasExpiration && (
            <DateInput label="Vencimento" withAsterisk valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable value={expiresOn} onChange={setExpiresOn}
              minDate={issuedOn ?? undefined} error={errors.expiresOn}
              description={type ? `Alerta ${type.alertDaysBefore} dias antes` : undefined} />
          )}
        </SimpleGrid>
        {!hasExpiration && <Text size="xs" c="dimmed">Este tipo de documento não tem validade.</Text>}
        <Textarea label="Observações" autosize minRows={2} maxLength={1000} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />

        <div>
          <Text size="sm" fw={500} mb={4}>Arquivos</Text>
          <Stack gap="xs">
            {mode?.kind === 'edit' && (
              <AttachmentList files={mode.document.files} onRemove={(f) => removeFile.mutate(f.id, {
                onSuccess: () => notifySuccess('Arquivo removido.'), onError: (e) => notifyError(e, 'O arquivo não foi removido'),
              })} />
            )}
            <AttachmentList files={newFiles} onRemove={(f) => setNewFiles((files) => files.filter((x) => x.id !== f.id))} />
            <Group gap="xs">
              <UploadButton onUploaded={(f) => setNewFiles((files) => [...files, f])} />
              <UploadButton camera onUploaded={(f) => setNewFiles((files) => [...files, f])} />
            </Group>
            <Text size="xs" c="dimmed">PDF, JPG ou PNG, até 10 MB.</Text>
            {errors.fileIds && <Text size="xs" c="red">{errors.fileIds}</Text>}
          </Stack>
        </div>

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose} disabled={saving}>Cancelar</Button>
          <Button onClick={submit} loading={saving} disabled={!typeId || (hasExpiration && !expiresOn)}>
            {mode?.kind === 'edit' ? 'Salvar alterações' : mode?.kind === 'renew' ? 'Renovar documento' : 'Cadastrar documento'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
