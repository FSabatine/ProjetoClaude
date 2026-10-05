import { useState, type ReactNode } from 'react';
import { Button, Group, Modal, Paper, Select, Stack, Switch, Table, TextInput, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconPlus } from '@tabler/icons-react';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { RowActions } from '../../components/common';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { costCentersApi } from './api';
import type { CostCenter } from './finance';

export function CostCentersPage() {
  const { can } = useAuth();
  const query = costCentersApi.useList(true);
  const removeMutation = costCentersApi.useRemove();
  const [editing, setEditing] = useState<CostCenter | 'new' | null>(null);
  const canManage = can(PERMISSIONS.finance.managecostcenters);

  const newButton = canManage && <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing('new')}>Novo centro de custo</Button>;

  if (query.error && !query.data) return <><PageHeader title="Centros de custo" action={newButton} /><Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper></>;

  const centers = query.data ?? [];
  const roots = centers.filter((c) => !c.parentCostCenterId);
  const childrenOf = (id: string) => centers.filter((c) => c.parentCostCenterId === id);

  const rows = (list: CostCenter[], depth: number): ReactNode[] =>
    list.flatMap((c) => [
      <Table.Tr key={c.id}>
        <Table.Td style={{ paddingLeft: 16 + depth * 20 }}>{c.name}</Table.Td>
        <Table.Td>{c.code}</Table.Td>
        <Table.Td>{c.isActive ? 'Ativo' : 'Inativo'}</Table.Td>
        <Table.Td>
          {canManage && (
            <RowActions onEdit={() => setEditing(c)} onDelete={() => confirmDelete({
              entity: 'o centro de custo', name: c.name, onConfirm: () => removeMutation.mutate(c.id, {
                onError: (e) => notifyError(e, 'O centro de custo não foi excluído'),
                onSuccess: () => notifySuccess('Centro de custo excluído.'),
              }),
            })} />
          )}
        </Table.Td>
      </Table.Tr>,
      ...rows(childrenOf(c.id), depth + 1),
    ]);

  return (
    <>
      <PageHeader title="Centros de custo" description="Onde a despesa pertence — departamento, filial ou projeto. Pode ter hierarquia." action={newButton} />
      {centers.length === 0 ? (
        <Paper><EmptyState icon={<IconPlus size={28} />} title="Nenhum centro de custo" description="Crie centros de custo para separar os gastos por área da empresa." action={newButton} /></Paper>
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
      {editing && <CostCenterModal center={editing === 'new' ? null : editing} centers={centers} onClose={() => setEditing(null)} />}
    </>
  );
}

function CostCenterModal({ center, centers, onClose }: { center: CostCenter | null; centers: CostCenter[]; onClose: () => void }) {
  const mutation = costCentersApi.useSave(center?.id);
  const initialValues = {
    name: center?.name ?? '', code: center?.code ?? '', description: center?.description ?? '',
    parentCostCenterId: center?.parentCostCenterId ?? null, isActive: center?.isActive ?? true,
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validate: {
      name: (v) => (v.trim() ? null : 'Nome: campo obrigatório.'),
      code: (v) => (v.trim() ? null : 'Código: campo obrigatório.'),
    },
  });
  const parentOptions = centers.filter((c) => c.id !== center?.id).map((c) => ({ value: c.id, label: c.name }));

  return (
    <Modal opened onClose={onClose} title={center ? 'Editar centro de custo' : 'Novo centro de custo'} centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate(
        { name: v.name.trim(), code: v.code.trim(), description: v.description.trim() || null, parentCostCenterId: v.parentCostCenterId, isActive: v.isActive },
        { onSuccess: () => { notifySuccess('Centro de custo salvo com sucesso.'); onClose(); }, onError: (e) => { const err = notifyError(e, 'O centro de custo não foi salvo'); form.setErrors(err.fieldErrors); } },
      ))}>
        <Stack gap="md">
          <TextInput label="Nome" withAsterisk {...form.getInputProps('name')} />
          <TextInput label="Código" withAsterisk {...form.getInputProps('code')} />
          <Select label="Centro de custo pai" data={parentOptions} clearable allowDeselect {...form.getInputProps('parentCostCenterId')} />
          <Textarea label="Descrição" autosize minRows={2} {...form.getInputProps('description')} />
          <Switch label="Ativo" {...form.getInputProps('isActive', { type: 'checkbox' })} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>Salvar</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
