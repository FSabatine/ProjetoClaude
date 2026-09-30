import { useState } from 'react';
import { Button, Group, Stack, Text } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { IconAlertTriangle, IconPlus } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { DriverPicker, VehiclePicker } from '../../components/EntityPickers';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatDateTime, formatPlate, fromApiDate, toApiDate } from '../../lib/format';
import { occurrencesApi, type OccurrenceListItem } from '../operations/api';
import { OCCURRENCE_SEVERITY, OCCURRENCE_STATUS, OCCURRENCE_TYPE } from '../operations/labels';
import { OccurrenceFormModal } from './OccurrenceFormModal';

const FILTERS = ['status', 'severity', 'type', 'vehicleId', 'driverId', 'from', 'to', 'openOnly'] as const;

export function OccurrenceListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'occurredAt', direction: 'Desc' });
  const query = occurrencesApi.useList(list.apiParams);
  const [creating, setCreating] = useState(false);

  const newButton = can(PERMISSIONS.occurrences.create) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => setCreating(true)}>Registrar ocorrência</Button>
  );

  const columns: Column<OccurrenceListItem>[] = [
    { key: 'date', header: 'Data', sortKey: 'occurredAt', render: (o) => <Text size="sm">{formatDateTime(o.occurredAt)}</Text> },
    {
      key: 'what', header: 'Ocorrência', render: (o) => (
        <div style={{ maxWidth: 420 }}>
          <Text size="sm" fw={600}>{OCCURRENCE_TYPE[o.type].label}{o.source === 'Checklist' && <Text span size="xs" c="dimmed"> · checklist</Text>}</Text>
          <Text size="xs" c="dimmed" lineClamp={1}>{o.description}</Text>
        </div>
      ),
    },
    {
      key: 'vehicle', header: 'Veículo / motorista', render: (o) => (
        <div>
          <Text size="sm" ff="monospace">{o.licensePlate ? formatPlate(o.licensePlate) : '—'}</Text>
          {o.driverName && <Text size="xs" c="dimmed">{o.driverName}</Text>}
        </div>
      ),
    },
    { key: 'severity', header: 'Gravidade', sortKey: 'severity', render: (o) => <StatusBadge value={o.severity} map={OCCURRENCE_SEVERITY} /> },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (o) => <StatusBadge value={o.status} map={OCCURRENCE_STATUS} /> },
  ];

  return (
    <>
      <PageHeader title="Ocorrências" description="Problemas, avarias e observações da operação — inclusive os itens reprovados nos checklists." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar na descrição, placa ou motorista"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(OCCURRENCE_STATUS), value: list.filters.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'severity', placeholder: 'Gravidade', data: toSelectData(OCCURRENCE_SEVERITY), value: list.filters.severity, onChange: (v) => list.setFilter('severity', v) },
          { key: 'type', placeholder: 'Tipo', data: toSelectData(OCCURRENCE_TYPE), value: list.filters.type, onChange: (v) => list.setFilter('type', v) },
        ]}
      />
      <Group gap="sm" mb="md" mt={-8} align="flex-end" wrap="wrap">
        <VehiclePicker size="xs" label="Veículo" value={list.filters.vehicleId} onChange={(v) => list.setFilter('vehicleId', v)} w={220} />
        <DriverPicker size="xs" label="Motorista" excludeInactive={false} value={list.filters.driverId} onChange={(v) => list.setFilter('driverId', v)} w={220} />
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
        getRowId={(o) => o.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(o) => navigate(`/ocorrencias/${o.id}`)}
        renderCard={(o) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={600} size="sm">{OCCURRENCE_TYPE[o.type].label}</Text>
              <StatusBadge value={o.status} map={OCCURRENCE_STATUS} />
            </Group>
            <Text size="sm" lineClamp={2}>{o.description}</Text>
            <Group gap="xs">
              <StatusBadge value={o.severity} map={OCCURRENCE_SEVERITY} />
              <Text size="xs" c="dimmed">{o.licensePlate ? formatPlate(o.licensePlate) : ''} · {formatDateTime(o.occurredAt)}</Text>
            </Group>
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconAlertTriangle size={28} />} title="Nenhuma ocorrência registrada"
            description="Registre problemas, avarias e observações. Itens reprovados nos checklists também viram ocorrências." action={newButton} />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
      <OccurrenceFormModal opened={creating} onClose={() => setCreating(false)} />
    </>
  );
}
