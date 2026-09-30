import { useState } from 'react';
import {
  ActionIcon, Badge, Box, Button, Checkbox, Group, Paper, Select, SimpleGrid, Skeleton, Stack, Switch, Table, Text, TextInput, Textarea,
} from '@mantine/core';
import { IconArrowDown, IconArrowUp, IconPlus, IconTrash } from '@tabler/icons-react';
import { useNavigate, useParams } from 'react-router-dom';
import { toApiError } from '../../api/errors';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { FormActions, FormSection, confirmDelete, useUnsavedChangesGuard } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { checklistTemplatesApi, type ChecklistTemplate, type ChecklistTemplateItemRequest } from '../operations/api';
import {
  CHECKLIST_FREQUENCY, CHECKLIST_RESPONSE_TYPE, OCCURRENCE_SEVERITY, OCCURRENCE_TYPE,
  type ChecklistFrequency, type ChecklistResponseType, type OccurrenceSeverity, type OccurrenceType,
} from '../operations/labels';

export function ChecklistTemplateListPage() {
  const navigate = useNavigate();
  const query = checklistTemplatesApi.useList(true);
  const remove = checklistTemplatesApi.useRemove();
  const newButton = <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/configuracoes/checklists/novo')}>Novo modelo</Button>;

  return (
    <>
      <PageHeader title="Modelos de checklist" description="Os itens que cada inspeção verifica. Alterar um modelo não muda os checklists já realizados."
        breadcrumbs={[{ label: 'Configurações' }, { label: 'Modelos de checklist' }]} action={newButton} />
      {query.isLoading && <Skeleton height={200} />}
      {query.error && <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper>}
      {query.data?.length === 0 && (
        <Paper><EmptyState icon={<IconPlus size={28} />} title="Nenhum modelo" description="Crie o primeiro modelo, por exemplo a inspeção diária antes do uso." action={newButton} /></Paper>
      )}
      {query.data && query.data.length > 0 && (
        <Paper style={{ overflow: 'hidden' }}>
          <Table.ScrollContainer minWidth={560}>
            <Table highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr><Table.Th>Modelo</Table.Th><Table.Th>Frequência</Table.Th><Table.Th>Itens</Table.Th><Table.Th>Realizados</Table.Th><Table.Th w={56} aria-label="Ações" /></Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {query.data.map((t) => (
                  <Table.Tr key={t.id} style={{ cursor: 'pointer', opacity: t.isActive ? 1 : 0.6 }} onClick={() => navigate(`/configuracoes/checklists/${t.id}`)}>
                    <Table.Td>
                      <Text size="sm" fw={600}>{t.name} <Text span size="xs" c="dimmed">v{t.version}</Text></Text>
                      {!t.isActive && <Badge size="xs" color="gray">Inativo</Badge>}
                      {t.description && <Text size="xs" c="dimmed">{t.description}</Text>}
                    </Table.Td>
                    <Table.Td>{CHECKLIST_FREQUENCY[t.frequency].label}</Table.Td>
                    <Table.Td>{t.itemCount}</Table.Td>
                    <Table.Td>{t.executionCount}</Table.Td>
                    <Table.Td onClick={(e) => e.stopPropagation()}>
                      <ActionIcon variant="subtle" color="red" aria-label={`Excluir ${t.name}`} onClick={() => confirmDelete({
                        entity: 'modelo', name: t.name,
                        onConfirm: () => remove.mutate(t.id, { onSuccess: () => notifySuccess('Modelo excluído.'), onError: (e) => notifyError(e, 'O modelo não foi excluído') }),
                      })}><IconTrash size={16} /></ActionIcon>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Paper>
      )}
    </>
  );
}

export function ChecklistTemplateFormPage() {
  const { id } = useParams();
  const detail = checklistTemplatesApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(t) => <TemplateForm template={t} />}</EntityFormPage>;
}

type ItemState = ChecklistTemplateItemRequest & { key: string };

const newItem = (): ItemState => ({
  key: crypto.randomUUID(), id: null, section: '', label: '', responseType: 'PassFail', isRequired: true, unit: null,
  requiresPhotoOnFail: false, failureOccurrenceType: 'MechanicalIssue', failureSeverity: 'Medium',
});

function TemplateForm({ template }: { template?: ChecklistTemplate }) {
  const navigate = useNavigate();
  const save = checklistTemplatesApi.useSave(template?.id);
  const [name, setName] = useState(template?.name ?? '');
  const [description, setDescription] = useState(template?.description ?? '');
  const [frequency, setFrequency] = useState<ChecklistFrequency>(template?.frequency ?? 'Daily');
  const [isActive, setIsActive] = useState(template?.isActive ?? true);
  const [items, setItems] = useState<ItemState[]>(() => template?.items.map((i) => ({ ...i, key: i.id, section: i.section ?? '' })) ?? [newItem()]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [dirty, setDirty] = useState(false);
  const guard = useUnsavedChangesGuard(dirty);

  const touch = <T,>(setter: (v: T) => void) => (v: T) => { setter(v); setDirty(true); };
  const update = (key: string, change: Partial<ItemState>) => { setItems((all) => all.map((i) => (i.key === key ? { ...i, ...change } : i))); setDirty(true); };
  const move = (index: number, delta: number) => {
    setItems((all) => { const next = [...all]; const [item] = next.splice(index, 1); next.splice(index + delta, 0, item); return next; });
    setDirty(true);
  };

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    save.mutate({
      name, description: description || null, frequency, isActive,
      items: items.map((i) => ({
        id: i.id, section: i.section || null, label: i.label, responseType: i.responseType, isRequired: i.isRequired, unit: i.unit,
        requiresPhotoOnFail: i.requiresPhotoOnFail, failureOccurrenceType: i.failureOccurrenceType, failureSeverity: i.failureSeverity,
      })),
    }, {
      onSuccess: (saved) => {
        guard.release();
        setDirty(false);
        notifySuccess(template && saved.version > template.version ? `Modelo salvo (nova versão ${saved.version}).` : 'Modelo salvo com sucesso.');
        navigate('/configuracoes/checklists');
      },
      onError: (err) => { setErrors(toApiError(err).fieldErrors); notifyError(err, 'O modelo não foi salvo'); },
    });
  };

  return (
    <>
      <PageHeader title={template ? template.name : 'Novo modelo de checklist'}
        description={template ? `Versão ${template.version}. Alterar os itens cria uma nova versão; checklists já realizados não mudam.` : 'Campos com * são obrigatórios.'}
        breadcrumbs={[{ label: 'Modelos de checklist', to: '/configuracoes/checklists' }, { label: template ? 'Editar' : 'Novo' }]} />
      <form onSubmit={submit} noValidate>
        <Stack gap="md">
          <FormSection title="Identificação" cols={2}>
            <TextInput label="Nome" withAsterisk maxLength={100} value={name} onChange={(e) => touch(setName)(e.currentTarget.value)} error={errors.name} />
            <Select label="Frequência" withAsterisk data={toSelectData(CHECKLIST_FREQUENCY)} value={frequency} onChange={(v) => touch(setFrequency)(v as ChecklistFrequency)}
              description="Diário/semanal: os veículos com motorista que não fizerem aparecem como pendentes." />
            <Textarea label="Descrição" autosize minRows={2} maxLength={500} value={description} onChange={(e) => touch(setDescription)(e.currentTarget.value)} />
            <Switch label="Ativo (disponível para novos checklists)" checked={isActive} onChange={(e) => touch(setIsActive)(e.currentTarget.checked)} mt="lg" />
          </FormSection>

          <FormSection title={`Itens (${items.length})`} description="Agrupe por seção (Pneus, Iluminação…). Cada item reprovado abre uma ocorrência com o tipo e a gravidade escolhidos." cols={1}>
            <Stack gap="sm">
              {errors.items && <Text c="red" size="sm">{errors.items}</Text>}
              {items.map((item, index) => (
                <Paper key={item.key} p="sm" withBorder>
                  <Stack gap="xs">
                    <Group gap="xs" wrap="nowrap" align="flex-start">
                      <Text size="sm" c="dimmed" w={24} mt={30}>{index + 1}</Text>
                      <SimpleGrid cols={{ base: 1, sm: 3 }} style={{ flex: 1 }}>
                        <TextInput label="Seção" placeholder="Ex.: Pneus" maxLength={60} value={item.section ?? ''} onChange={(e) => update(item.key, { section: e.currentTarget.value })} />
                        <TextInput label="Pergunta" withAsterisk maxLength={200} value={item.label} onChange={(e) => update(item.key, { label: e.currentTarget.value })}
                          error={errors[`items[${index}].label`]} style={{ gridColumn: 'span 2' }} />
                      </SimpleGrid>
                      <Group gap={2} mt={24} wrap="nowrap">
                        <ActionIcon variant="subtle" color="gray" disabled={index === 0} onClick={() => move(index, -1)} aria-label="Mover para cima"><IconArrowUp size={16} /></ActionIcon>
                        <ActionIcon variant="subtle" color="gray" disabled={index === items.length - 1} onClick={() => move(index, 1)} aria-label="Mover para baixo"><IconArrowDown size={16} /></ActionIcon>
                        <ActionIcon variant="subtle" color="red" disabled={items.length === 1} onClick={() => { setItems((all) => all.filter((i) => i.key !== item.key)); setDirty(true); }} aria-label="Remover item"><IconTrash size={16} /></ActionIcon>
                      </Group>
                    </Group>
                    <Box pl={32}>
                      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
                        <Select label="Resposta" size="xs" data={toSelectData(CHECKLIST_RESPONSE_TYPE)} value={item.responseType}
                          onChange={(v) => update(item.key, { responseType: v as ChecklistResponseType })} />
                        {item.responseType === 'PassFail' ? (
                          <>
                            <Select label="Se não conforme, abrir" size="xs" data={toSelectData(OCCURRENCE_TYPE)} value={item.failureOccurrenceType}
                              onChange={(v) => update(item.key, { failureOccurrenceType: v as OccurrenceType })} />
                            <Select label="Gravidade padrão" size="xs" data={toSelectData(OCCURRENCE_SEVERITY)} value={item.failureSeverity}
                              onChange={(v) => update(item.key, { failureSeverity: v as OccurrenceSeverity })} />
                          </>
                        ) : item.responseType === 'Number' ? (
                          <TextInput label="Unidade" size="xs" placeholder="%, bar, mm" maxLength={20} value={item.unit ?? ''} onChange={(e) => update(item.key, { unit: e.currentTarget.value || null })} />
                        ) : null}
                        <Stack gap={4} mt={item.responseType === 'Text' ? 0 : 18}>
                          <Checkbox size="xs" label="Obrigatório" checked={item.isRequired} onChange={(e) => update(item.key, { isRequired: e.currentTarget.checked })} />
                          {item.responseType === 'PassFail' && (
                            <Checkbox size="xs" label="Exigir foto se não conforme" checked={item.requiresPhotoOnFail} onChange={(e) => update(item.key, { requiresPhotoOnFail: e.currentTarget.checked })} />
                          )}
                        </Stack>
                      </SimpleGrid>
                    </Box>
                  </Stack>
                </Paper>
              ))}
              <Button variant="default" leftSection={<IconPlus size={16} />} onClick={() => { setItems((all) => [...all, { ...newItem(), section: all[all.length - 1]?.section ?? '' }]); setDirty(true); }}>
                Adicionar item
              </Button>
            </Stack>
          </FormSection>
        </Stack>
        <Box mt="md">
          <FormActions saving={save.isPending} onCancel={() => navigate('/configuracoes/checklists')} submitLabel={template ? 'Salvar modelo' : 'Criar modelo'} />
        </Box>
      </form>
    </>
  );
}
