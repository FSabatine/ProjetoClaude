import { Button, Group, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { IconChecklist, IconPlayerPlay } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { VehiclePicker } from '../../components/EntityPickers';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatDateTime, formatPlate, fromApiDate, toApiDate } from '../../lib/format';
import { checklistTemplatesApi, checklistsApi, type ChecklistListItem } from '../operations/api';
import { CHECKLIST_FREQUENCY, CHECKLIST_RESULT } from '../operations/labels';

const FILTERS = ['vehicleId', 'templateId', 'result', 'from', 'to'] as const;

/** Pending recurring checklists (what is owed today) on top, the inspection history below. */
export function ChecklistListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const canExecute = can(PERMISSIONS.checklists.execute);
  const list = useListParams(FILTERS, { by: 'performedAt', direction: 'Desc' });
  const query = checklistsApi.useList(list.apiParams);
  const pending = checklistsApi.usePending();
  const templates = checklistTemplatesApi.useList(true);

  const startButton = canExecute && (
    <Button leftSection={<IconPlayerPlay size={18} />} onClick={() => navigate('/checklists/realizar')}>Realizar checklist</Button>
  );

  const columns: Column<ChecklistListItem>[] = [
    { key: 'date', header: 'Data', render: (c) => <Text size="sm">{formatDateTime(c.performedAt)}</Text> },
    { key: 'vehicle', header: 'Veículo', render: (c) => <Text size="sm" fw={600} ff="monospace">{formatPlate(c.licensePlate)}</Text> },
    { key: 'template', header: 'Modelo', render: (c) => <Text size="sm">{c.templateName}</Text> },
    { key: 'driver', header: 'Motorista', secondary: true, render: (c) => <Text size="sm">{c.driverName ?? '—'}</Text> },
    { key: 'by', header: 'Realizado por', secondary: true, render: (c) => <Text size="sm" c="dimmed">{c.performedByName ?? '—'}</Text> },
    {
      key: 'result', header: 'Resultado', render: (c) => (
        <Group gap={6}>
          <StatusBadge value={c.result} map={CHECKLIST_RESULT} />
          {c.failedItems > 0 && <Text size="xs" c="red">{c.failedItems}</Text>}
        </Group>
      ),
    },
  ];

  return (
    <>
      <PageHeader title="Checklists" description="Inspeções dos veículos antes e depois do uso." action={startButton} />

      {pending.data && pending.data.length > 0 && (
        <Paper p="md" mb="md">
          <Title order={4} fz="md" mb="sm">Pendentes hoje ({pending.data.length})</Title>
          <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
            {pending.data.slice(0, 9).map((p) => (
              <Paper key={`${p.vehicleId}-${p.templateId}`} p="sm" withBorder>
                <Group justify="space-between" wrap="nowrap">
                  <div style={{ minWidth: 0 }}>
                    <Text fw={600} ff="monospace">{formatPlate(p.licensePlate)}</Text>
                    <Text size="xs" c="dimmed" truncate>{p.templateName} · {CHECKLIST_FREQUENCY[p.frequency].label}{p.driverName ? ` · ${p.driverName}` : ''}</Text>
                  </div>
                  {canExecute && (
                    <Button size="xs" variant="light" onClick={() => navigate(`/checklists/realizar?veiculo=${p.vehicleId}&modelo=${p.templateId}`)}>Iniciar</Button>
                  )}
                </Group>
              </Paper>
            ))}
          </SimpleGrid>
        </Paper>
      )}

      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por placa, modelo ou motorista"
        filters={[
          { key: 'result', placeholder: 'Resultado', data: toSelectData(CHECKLIST_RESULT), value: list.filters.result, onChange: (v) => list.setFilter('result', v) },
          {
            key: 'templateId', placeholder: 'Modelo', value: list.filters.templateId, onChange: (v) => list.setFilter('templateId', v),
            data: (templates.data ?? []).map((t) => ({ value: t.id, label: t.name })),
          },
        ]}
      />
      <Group gap="sm" mb="md" mt={-8} align="flex-end" wrap="wrap">
        <VehiclePicker size="xs" label="Veículo" value={list.filters.vehicleId} onChange={(v) => list.setFilter('vehicleId', v)} w={220} />
        <DateInput size="xs" label="De" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable w={140}
          value={fromApiDate(list.filters.from)} onChange={(v) => list.setFilter('from', toApiDate(v))} />
        <DateInput size="xs" label="Até" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" clearable w={140}
          value={fromApiDate(list.filters.to)} onChange={(v) => list.setFilter('to', toApiDate(v))} />
      </Group>
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
        onRowClick={(c) => navigate(`/checklists/${c.id}`)}
        renderCard={(c) => (
          <Stack gap={4}>
            <Group justify="space-between" wrap="nowrap">
              <Text fw={700} ff="monospace">{formatPlate(c.licensePlate)}</Text>
              <StatusBadge value={c.result} map={CHECKLIST_RESULT} />
            </Group>
            <Text size="sm">{c.templateName}</Text>
            <Text size="xs" c="dimmed">{formatDateTime(c.performedAt)}{c.driverName ? ` · ${c.driverName}` : ''}</Text>
          </Stack>
        )}
        emptyState={<EmptyState icon={<IconChecklist size={28} />} title="Nenhum checklist realizado" description="As inspeções realizadas aparecerão aqui." action={startButton} />}
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
