import { useState } from 'react';
import { ActionIcon, Badge, Button, Group, Modal, NumberInput, Paper, Select, Skeleton, Stack, Switch, Table, Text, TextInput } from '@mantine/core';
import { IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { toApiError } from '../../api/errors';
import { toSelectData } from '../../components/common';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { ErrorState } from '../../components/States';
import { documentTypesApi, type DocumentType } from '../operations/api';
import { DOCUMENT_OWNER_TYPE, type DocumentOwnerType } from '../operations/labels';

interface FormState {
  id?: string;
  name: string;
  ownerType: DocumentOwnerType | null;
  hasExpiration: boolean;
  alertDaysBefore: number | string;
  isActive: boolean;
}

const EMPTY: FormState = { name: '', ownerType: 'Vehicle', hasExpiration: true, alertDaysBefore: 30, isActive: true };

/** Configurable catalog (ADR-021): new kinds of document without code changes. */
export function DocumentTypesPage() {
  const query = documentTypesApi.useList(null, true);
  const save = documentTypesApi.useSave();
  const remove = documentTypesApi.useRemove();
  const [form, setForm] = useState<FormState | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const open = (t?: DocumentType) => {
    setErrors({});
    setForm(t ? { ...t } : { ...EMPTY });
  };

  const submit = () =>
    form &&
    save.mutate({ ...form, ownerType: form.ownerType!, alertDaysBefore: Number(form.alertDaysBefore) || 0 }, {
      onSuccess: () => { notifySuccess('Tipo de documento salvo.'); setForm(null); },
      onError: (e) => { setErrors(toApiError(e).fieldErrors); notifyError(e, 'O tipo não foi salvo'); },
    });

  const groups = (Object.keys(DOCUMENT_OWNER_TYPE) as DocumentOwnerType[]).map((owner) => ({
    owner,
    types: (query.data ?? []).filter((t) => t.ownerType === owner),
  }));

  return (
    <>
      <PageHeader
        title="Tipos de documento"
        description="Quais documentos a frota controla, se vencem e com quantos dias de antecedência alertar."
        breadcrumbs={[{ label: 'Configurações' }, { label: 'Tipos de documento' }]}
        action={<Button leftSection={<IconPlus size={18} />} onClick={() => open()}>Novo tipo</Button>}
      />
      {query.isLoading && <Skeleton height={300} />}
      {query.error && <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper>}
      <Stack gap="md">
        {query.data && groups.map(({ owner, types }) => (
          <Paper key={owner} p="md">
            <Text fw={650} mb="sm">{DOCUMENT_OWNER_TYPE[owner].label}</Text>
            {types.length === 0 ? <Text size="sm" c="dimmed">Nenhum tipo.</Text> : (
              <Table.ScrollContainer minWidth={480}>
                <Table verticalSpacing="xs">
                  <Table.Thead>
                    <Table.Tr><Table.Th>Nome</Table.Th><Table.Th>Validade</Table.Th><Table.Th>Documentos</Table.Th><Table.Th w={90} aria-label="Ações" /></Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {types.map((t) => (
                      <Table.Tr key={t.id} style={{ opacity: t.isActive ? 1 : 0.6 }}>
                        <Table.Td>
                          <Text size="sm" fw={500}>{t.name}</Text>
                          {!t.isActive && <Badge color="gray" size="xs">Inativo</Badge>}
                        </Table.Td>
                        <Table.Td><Text size="sm">{t.hasExpiration ? `Alerta ${t.alertDaysBefore} dias antes` : 'Sem validade'}</Text></Table.Td>
                        <Table.Td><Text size="sm">{t.documentCount}</Text></Table.Td>
                        <Table.Td>
                          <Group gap={4} wrap="nowrap">
                            <ActionIcon variant="subtle" color="gray" aria-label={`Editar ${t.name}`} onClick={() => open(t)}><IconPencil size={16} /></ActionIcon>
                            {t.documentCount === 0 && (
                              <ActionIcon variant="subtle" color="red" aria-label={`Excluir ${t.name}`} onClick={() => confirmDelete({
                                entity: 'tipo de documento', name: t.name,
                                onConfirm: () => remove.mutate(t.id, { onSuccess: () => notifySuccess('Tipo excluído.'), onError: (e) => notifyError(e, 'O tipo não foi excluído') }),
                              })}><IconTrash size={16} /></ActionIcon>
                            )}
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            )}
          </Paper>
        ))}
      </Stack>

      <Modal opened={!!form} onClose={() => setForm(null)} title={form?.id ? 'Editar tipo de documento' : 'Novo tipo de documento'} centered>
        {form && (
          <Stack>
            <TextInput label="Nome" withAsterisk maxLength={80} value={form.name} error={errors.name} data-autofocus
              onChange={(e) => setForm({ ...form, name: e.currentTarget.value })} />
            <Select label="Aplica-se a" withAsterisk data={toSelectData(DOCUMENT_OWNER_TYPE)} value={form.ownerType} error={errors.ownerType}
              onChange={(v) => setForm({ ...form, ownerType: v as DocumentOwnerType })} />
            <Switch label="Tem data de vencimento" checked={form.hasExpiration} onChange={(e) => setForm({ ...form, hasExpiration: e.currentTarget.checked })} />
            {form.hasExpiration && (
              <NumberInput label="Alertar com antecedência de" suffix=" dias" min={0} max={365} allowDecimal={false} value={form.alertDaysBefore}
                error={errors.alertDaysBefore} onChange={(v) => setForm({ ...form, alertDaysBefore: v })}
                description="A partir dessa data o documento aparece como 'Vencendo' no painel." />
            )}
            <Switch label="Ativo (disponível para novos documentos)" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.currentTarget.checked })} />
            <Group justify="flex-end">
              <Button variant="default" onClick={() => setForm(null)}>Cancelar</Button>
              <Button onClick={submit} loading={save.isPending} disabled={!form.name.trim()}>Salvar tipo</Button>
            </Group>
          </Stack>
        )}
      </Modal>
    </>
  );
}
