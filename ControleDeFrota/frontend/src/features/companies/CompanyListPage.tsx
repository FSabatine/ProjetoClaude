import { Badge, Button, Group, Stack, Text } from '@mantine/core';
import { IconBuildingSkyscraper, IconPlus } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { ListToolbar, RowActions } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatCnpj } from '../../lib/format';
import { companiesApi, type CompanyListItem } from './companies';

const FILTERS = ['isActive'] as const;
const ACTIVE_FILTER = [
  { value: 'true', label: 'Ativas' },
  { value: 'false', label: 'Inativas' },
];

const activeBadge = (c: CompanyListItem) => <Badge color={c.isActive ? 'teal' : 'gray'}>{c.isActive ? 'Ativa' : 'Inativa'}</Badge>;

/** Platform administration (companies.manage) — ADR-003. */
export function CompanyListPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const list = useListParams(FILTERS, { by: 'legalName', direction: 'Asc' });
  const query = companiesApi.useList(list.apiParams);
  const remove = companiesApi.useRemove();

  const newButton = (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/empresas/nova')}>
      Nova empresa
    </Button>
  );

  const handleDelete = (c: CompanyListItem) =>
    confirmDelete({
      entity: 'empresa',
      name: c.tradeName ?? c.legalName,
      onConfirm: () =>
        remove.mutate(c.id, {
          onSuccess: () => notifySuccess('Empresa excluída.'),
          onError: (e) => notifyError(e, 'A empresa não foi excluída'),
        }),
    });

  const columns: Column<CompanyListItem>[] = [
    {
      key: 'name',
      header: 'Empresa',
      sortKey: 'legalName',
      render: (c) => (
        <div>
          <Text size="sm" fw={600}>{c.tradeName ?? c.legalName}</Text>
          {c.tradeName && <Text size="xs" c="dimmed">{c.legalName}</Text>}
        </div>
      ),
    },
    { key: 'cnpj', header: 'CNPJ', sortKey: 'cnpj', render: (c) => <Text size="sm" ff="monospace">{formatCnpj(c.cnpj)}</Text> },
    { key: 'city', header: 'Cidade', sortKey: 'city', secondary: true, render: (c) => (c.city ? `${c.city}/${c.state}` : '—') },
    { key: 'users', header: 'Usuários', align: 'right', secondary: true, render: (c) => c.userCount },
    { key: 'status', header: 'Situação', render: activeBadge },
  ];

  return (
    <>
      <PageHeader title="Empresas" description="Empresas atendidas pela plataforma. Cada uma enxerga apenas os próprios dados." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por razão social, nome fantasia ou CNPJ"
        filters={[{ key: 'isActive', placeholder: 'Situação', data: ACTIVE_FILTER, value: list.filters.isActive, onChange: (v) => list.setFilter('isActive', v) }]}
      />
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(c) => c.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(c) => navigate(`/empresas/${c.id}`)}
        rowActions={(c) => (
          <RowActions onEdit={() => navigate(`/empresas/${c.id}`)} onDelete={c.id !== user?.companyId ? () => handleDelete(c) : undefined} />
        )}
        renderCard={(c) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={700} truncate>{c.tradeName ?? c.legalName}</Text>
              {activeBadge(c)}
            </Group>
            <Text size="sm" ff="monospace">{formatCnpj(c.cnpj)}</Text>
            <Text size="xs" c="dimmed">{c.city ? `${c.city}/${c.state}` : ''} · {c.userCount} usuário(s)</Text>
          </Stack>
        )}
        emptyState={<EmptyState icon={<IconBuildingSkyscraper size={28} />} title="Nenhuma empresa" description="Cadastre a primeira empresa da plataforma." action={newButton} />}
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
