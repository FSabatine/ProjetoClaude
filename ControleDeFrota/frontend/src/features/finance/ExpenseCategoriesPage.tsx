import { useState, type ReactNode } from 'react';
import { Badge, Button, Group, Modal, Paper, Select, Stack, Switch, Table, TextInput, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconPlus } from '@tabler/icons-react';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { RowActions } from '../../components/common';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { expenseCategoriesApi } from './api';
import type { ExpenseCategory } from './finance';

export function ExpenseCategoriesPage() {
  const { can } = useAuth();
  const query = expenseCategoriesApi.useList(true);
  const removeMutation = expenseCategoriesApi.useRemove();
  const [editing, setEditing] = useState<ExpenseCategory | 'new' | null>(null);
  const canManage = can(PERMISSIONS.finance.managecategories);

  const newButton = canManage && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing('new')}>Nova categoria</Button>
  );

  if (query.error && !query.data) return <><PageHeader title="Categorias de despesa" action={newButton} /><Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper></>;

  const categories = query.data ?? [];
  const roots = categories.filter((c) => !c.parentCategoryId);
  const childrenOf = (id: string) => categories.filter((c) => c.parentCategoryId === id);

  const rows = (list: ExpenseCategory[], depth: number): ReactNode[] =>
    list.flatMap((c) => [
      <Table.Tr key={c.id}>
        <Table.Td style={{ paddingLeft: 16 + depth * 20 }}>{c.name}{c.isSystemCategory && <Badge ml="xs" size="sm" variant="light">Automática</Badge>}</Table.Td>
        <Table.Td>{c.code}</Table.Td>
        <Table.Td>{c.isActive ? 'Ativa' : 'Inativa'}</Table.Td>
        <Table.Td onClick={(e) => e.stopPropagation()}>
          {canManage && !c.isSystemCategory && (
            <RowActions onEdit={() => setEditing(c)} onDelete={() => confirmDelete({
              entity: 'a categoria', name: c.name, onConfirm: () => removeMutation.mutate(c.id, {
                onError: (e) => notifyError(e, 'A categoria não foi excluída'),
                onSuccess: () => notifySuccess('Categoria excluída.'),
              }),
            })} />
          )}
        </Table.Td>
      </Table.Tr>,
      ...rows(childrenOf(c.id), depth + 1),
    ]);

  return (
    <>
      <PageHeader title="Categorias de despesa" description="Combustível, Manutenção e Pneus são automáticas: alimentadas pelos próprios módulos." action={newButton} />
      {categories.length === 0 ? (
        <Paper><EmptyState icon={<IconPlus size={28} />} title="Nenhuma categoria" description="As categorias padrão aparecem automaticamente no primeiro acesso." /></Paper>
      ) : (
        <Paper>
          <Table.ScrollContainer minWidth={480}>
            <Table highlightOnHover>
              <Table.Thead><Table.Tr><Table.Th>Nome</Table.Th><Table.Th>Código</Table.Th><Table.Th>Situação</Table.Th><Table.Th style={{ width: 56 }} /></Table.Tr></Table.Thead>
              <Table.Tbody>{rows(roots, 0)}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Paper>
      )}
      {editing && (
        <CategoryModal
          category={editing === 'new' ? null : editing}
          categories={categories}
          onClose={() => setEditing(null)}
        />
      )}
    </>
  );
}

function CategoryModal({ category, categories, onClose }: { category: ExpenseCategory | null; categories: ExpenseCategory[]; onClose: () => void }) {
  const mutation = expenseCategoriesApi.useSave(category?.id);
  const initialValues = {
    name: category?.name ?? '', code: category?.code ?? '', description: category?.description ?? '',
    parentCategoryId: category?.parentCategoryId ?? null, isActive: category?.isActive ?? true,
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validate: {
      name: (v) => (v.trim() ? null : 'Nome: campo obrigatório.'),
      code: (v) => (v.trim() ? null : 'Código: campo obrigatório.'),
    },
  });
  const parentOptions = categories.filter((c) => c.id !== category?.id && !c.isSystemCategory).map((c) => ({ value: c.id, label: c.name }));

  return (
    <Modal opened onClose={onClose} title={category ? 'Editar categoria' : 'Nova categoria'} centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate(
        { name: v.name.trim(), code: v.code.trim(), description: v.description.trim() || null, parentCategoryId: v.parentCategoryId, isActive: v.isActive },
        { onSuccess: () => { notifySuccess('Categoria salva com sucesso.'); onClose(); }, onError: (e) => { const err = notifyError(e, 'A categoria não foi salva'); form.setErrors(err.fieldErrors); } },
      ))}>
        <Stack gap="md">
          <TextInput label="Nome" withAsterisk {...form.getInputProps('name')} />
          <TextInput label="Código" withAsterisk description="Letras, números e hífen." {...form.getInputProps('code')} />
          <Select label="Categoria-mãe" data={parentOptions} clearable allowDeselect {...form.getInputProps('parentCategoryId')} />
          <Textarea label="Descrição" autosize minRows={2} {...form.getInputProps('description')} />
          <Switch label="Ativa" {...form.getInputProps('isActive', { type: 'checkbox' })} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>Salvar</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
