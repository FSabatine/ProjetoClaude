import { Badge, Button, Group, Stack, Text } from '@mantine/core';
import { IconLock, IconPlus, IconUsers } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, RowActions, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatDateTime } from '../../lib/format';
import { USER_STATUS, usersApi, type UserListItem } from './users';

const FILTERS = ['status'] as const;

export function UserListPage() {
  const navigate = useNavigate();
  const { can, user: me } = useAuth();
  const list = useListParams(FILTERS, { by: 'name', direction: 'Asc' });
  const query = usersApi.useList(list.apiParams);
  const remove = usersApi.useRemove();
  const canManage = can(PERMISSIONS.users.manage);
  const showCompany = can(PERMISSIONS.companies.manage);

  const newButton = canManage && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/usuarios/novo')}>
      Novo usuário
    </Button>
  );

  const handleDelete = (u: UserListItem) =>
    confirmDelete({
      entity: 'usuário',
      name: `${u.name} (${u.email})`,
      onConfirm: () =>
        remove.mutate(u.id, {
          onSuccess: () => notifySuccess('Usuário excluído. As sessões dele foram encerradas.'),
          onError: (e) => notifyError(e, 'O usuário não foi excluído'),
        }),
    });

  const status = (u: UserListItem) => (
    <Group gap={6} wrap="nowrap">
      <StatusBadge value={u.status} map={USER_STATUS} />
      {u.isLockedOut && (
        <Badge color="red" leftSection={<IconLock size={12} />}>
          Bloqueado
        </Badge>
      )}
    </Group>
  );

  const columns: Column<UserListItem>[] = [
    {
      key: 'name',
      header: 'Nome',
      sortKey: 'name',
      render: (u) => (
        <div>
          <Text size="sm" fw={600}>{u.name}{u.id === me?.id && <Text span c="dimmed" fw={400}> (você)</Text>}</Text>
          <Text size="xs" c="dimmed">{u.email}</Text>
        </div>
      ),
    },
    ...(showCompany ? [{ key: 'company', header: 'Empresa', secondary: true, render: (u: UserListItem) => u.companyName }] : []),
    { key: 'roles', header: 'Papéis', render: (u) => <Text size="sm">{u.roles.join(', ')}</Text> },
    { key: 'lastLogin', header: 'Último acesso', sortKey: 'lastLoginAt', secondary: true, render: (u) => formatDateTime(u.lastLoginAt) },
    { key: 'status', header: 'Situação', sortKey: 'status', render: status },
  ];

  return (
    <>
      <PageHeader title="Usuários" description="Pessoas com acesso ao sistema e seus papéis." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por nome ou e-mail"
        filters={[{ key: 'status', placeholder: 'Situação', data: toSelectData(USER_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) }]}
      />
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(u) => u.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(u) => navigate(`/usuarios/${u.id}`)}
        rowActions={(u) => (
          <RowActions
            onEdit={canManage ? () => navigate(`/usuarios/${u.id}`) : undefined}
            onDelete={canManage && u.id !== me?.id ? () => handleDelete(u) : undefined}
          />
        )}
        renderCard={(u) => (
          <Stack gap={4}>
            <Text fw={700} truncate>{u.name}</Text>
            <Text size="sm" c="dimmed" truncate>{u.email}</Text>
            <Text size="xs">{u.roles.join(', ')}</Text>
            {status(u)}
          </Stack>
        )}
        emptyState={<EmptyState icon={<IconUsers size={28} />} title="Nenhum usuário" description="Cadastre as pessoas que vão usar o sistema." action={newButton} />}
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
