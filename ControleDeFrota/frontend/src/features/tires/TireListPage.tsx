import { Badge, Button, Group, NumberInput, Paper, Popover, Select, Stack, Text, TextInput } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconAdjustmentsHorizontal, IconAlertOctagon, IconAlertTriangle, IconCircleDot, IconPlus } from '@tabler/icons-react';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { ImplementPicker, VehiclePicker } from '../../components/EntityPickers';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatDate, formatNumber } from '../../lib/format';
import { formatTread } from '../../lib/tires';
import { tireModelsApi, tiresApi } from './api';
import { ALERT_FILTER, INSPECTION_FILTER, TIRE_STATUS, type TireListItem } from './tires';

const FILTERS = [
  'status', 'alert', 'inspection', 'brand', 'size', 'vehicleId', 'implementId', 'positionCode', 'storageLocation',
  'manufacturedFrom', 'manufacturedTo', 'installedFrom', 'installedTo', 'minRetreads', 'maxRetreads',
] as const;

export function locationText(t: Pick<TireListItem, 'location' | 'storageLocation' | 'status'>) {
  if (t.location) return `${t.location.assetLabel} · ${t.location.positionLabel}`;
  if (t.status === 'InStock' || t.status === 'UnderInspection') return t.storageLocation ?? 'Estoque (local não informado)';
  return TIRE_STATUS[t.status].label;
}

export function AlertMark({ count, severity }: { count: number; severity: 'Warning' | 'Critical' | null }) {
  if (count === 0) return <Text size="xs" c="dimmed">—</Text>;
  const critical = severity === 'Critical';
  return (
    <Badge color={critical ? 'red' : 'orange'} variant="light" leftSection={critical ? <IconAlertOctagon size={12} /> : <IconAlertTriangle size={12} />}>
      {count} {count === 1 ? 'alerta' : 'alertas'}
    </Badge>
  );
}

/** Inventory of individual tires (seções 40, 46): every filter is applied on the server and lives in the URL. */
export function TireListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'code', direction: 'Asc' });
  const query = tiresApi.useList(list.apiParams);
  const brands = tireModelsApi.useBrands();
  const f = list.filters;

  const newButton = can(PERMISSIONS.tires.create) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/pneus/novo')}>Cadastrar pneu</Button>
  );

  const columns: Column<TireListItem>[] = [
    { key: 'code', header: 'Nº de fogo', sortKey: 'code', render: (t) => <Text size="sm" fw={700} ff="monospace">{t.code}</Text> },
    { key: 'model', header: 'Marca / modelo', sortKey: 'brand', render: (t) => <Text size="sm">{t.brand} {t.modelName}</Text> },
    { key: 'size', header: 'Medida', sortKey: 'size', secondary: true, render: (t) => t.size },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (t) => <StatusBadge value={t.status} map={TIRE_STATUS} /> },
    { key: 'location', header: 'Onde está', render: (t) => <Text size="sm">{locationText(t)}</Text> },
    { key: 'tread', header: 'Sulco', sortKey: 'treadDepth', align: 'right', render: (t) => formatTread(t.currentTreadDepthMm) },
    { key: 'km', header: 'Km', sortKey: 'km', align: 'right', secondary: true, render: (t) => (t.currentKm !== null ? formatNumber(t.currentKm) : '—') },
    { key: 'retreads', header: 'Recap.', sortKey: 'retreads', align: 'right', secondary: true, render: (t) => t.retreadCount },
    { key: 'inspected', header: 'Última inspeção', sortKey: 'lastInspectedAt', secondary: true, render: (t) => (t.lastInspectedAt ? formatDate(t.lastInspectedAt) : 'nunca') },
    { key: 'alerts', header: 'Alertas', render: (t) => <AlertMark count={t.alertCount + t.openAnomalyCount} severity={t.topAlertSeverity ?? (t.openAnomalyCount ? 'Warning' : null)} /> },
  ];

  const more = [f.implementId, f.positionCode, f.storageLocation, f.manufacturedFrom, f.installedFrom, f.minRetreads, f.maxRetreads, f.size].filter(Boolean).length;
  const range = (from: string | null, to: string | null): [Date | null, Date | null] => [from ? dayjs(from).toDate() : null, to ? dayjs(to).toDate() : null];
  const setRange = (fromKey: (typeof FILTERS)[number], toKey: (typeof FILTERS)[number]) => ([start, end]: [Date | null, Date | null]) => {
    list.setFilter(fromKey, start ? dayjs(start).format('YYYY-MM-DD') : null);
    if (!start || end) list.setFilter(toKey, end ? dayjs(end).format('YYYY-MM-DD') : null);
  };

  return (
    <>
      <PageHeader title="Pneus" description="Cada pneu da frota pelo número de fogo: onde está, quanto rodou, sulco e alertas." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por nº de fogo, série, DOT, marca ou modelo"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(TIRE_STATUS), value: f.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'alert', placeholder: 'Alerta', data: toSelectData(ALERT_FILTER), value: f.alert, onChange: (v) => list.setFilter('alert', v) },
          { key: 'inspection', placeholder: 'Inspeção', data: toSelectData(INSPECTION_FILTER), value: f.inspection, onChange: (v) => list.setFilter('inspection', v) },
          { key: 'brand', placeholder: 'Marca', data: (brands.data ?? []).map((b) => ({ value: b, label: b })), value: f.brand, onChange: (v) => list.setFilter('brand', v) },
        ]}
      />
      <Paper p="sm" mb="md" mt={-8}>
        <Group gap="sm" wrap="wrap">
          <VehiclePicker aria-label="Veículo" placeholder="Instalado no veículo" value={f.vehicleId} onChange={(v) => list.setFilter('vehicleId', v)} style={{ flex: '1 1 220px' }} />
          <Popover width={340} position="bottom-end" shadow="md">
            <Popover.Target>
              <Button variant="default" leftSection={<IconAdjustmentsHorizontal size={16} />}
                rightSection={more > 0 ? <Badge size="sm" circle>{more}</Badge> : undefined}>Mais filtros</Button>
            </Popover.Target>
            <Popover.Dropdown>
              <Stack gap="sm">
                <ImplementPicker label="Instalado no implemento" value={f.implementId} onChange={(v) => list.setFilter('implementId', v)} />
                <Group grow>
                  <TextInput label="Posição" placeholder="Ex.: 2EE" value={f.positionCode ?? ''} onChange={(e) => list.setFilter('positionCode', e.currentTarget.value || null)} />
                  <TextInput label="Medida" placeholder="Ex.: 295/80R22.5" value={f.size ?? ''} onChange={(e) => list.setFilter('size', e.currentTarget.value || null)} />
                </Group>
                <TextInput label="Local de armazenamento" value={f.storageLocation ?? ''} onChange={(e) => list.setFilter('storageLocation', e.currentTarget.value || null)} />
                <DatePickerInput type="range" label="Fabricação" valueFormat="DD/MM/YYYY" clearable maxDate={new Date()}
                  value={range(f.manufacturedFrom, f.manufacturedTo)} onChange={setRange('manufacturedFrom', 'manufacturedTo')} />
                <DatePickerInput type="range" label="Instalado (posição atual)" valueFormat="DD/MM/YYYY" clearable maxDate={new Date()}
                  value={range(f.installedFrom, f.installedTo)} onChange={setRange('installedFrom', 'installedTo')} />
                <Group grow>
                  <NumberInput label="Recapagens mín." allowNegative={false} allowDecimal={false} value={f.minRetreads ?? ''}
                    onChange={(v) => list.setFilter('minRetreads', v === '' ? null : String(v))} />
                  <NumberInput label="Recapagens máx." allowNegative={false} allowDecimal={false} value={f.maxRetreads ?? ''}
                    onChange={(v) => list.setFilter('maxRetreads', v === '' ? null : String(v))} />
                </Group>
              </Stack>
            </Popover.Dropdown>
          </Popover>
          <Select aria-label="Atalho" placeholder="Atalhos" data={[{ value: 'stock', label: 'Disponíveis em estoque' }, { value: 'near', label: 'Perto da substituição' }]}
            value={null} onChange={(v) => v === 'stock' ? list.setFilter('status', 'InStock') : v === 'near' ? list.setFilter('alert', 'NearReplacement') : undefined}
            style={{ flex: '0 1 220px' }} />
        </Group>
      </Paper>
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(t) => t.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(t) => navigate(`/pneus/${t.id}`)}
        renderCard={(t) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={700} ff="monospace">{t.code}</Text>
              <StatusBadge value={t.status} map={TIRE_STATUS} />
            </Group>
            <Text size="sm">{t.brand} {t.modelName} · {t.size}</Text>
            <Text size="xs" c="dimmed">{locationText(t)} · sulco {formatTread(t.currentTreadDepthMm)}</Text>
            <AlertMark count={t.alertCount + t.openAnomalyCount} severity={t.topAlertSeverity ?? (t.openAnomalyCount ? 'Warning' : null)} />
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconCircleDot size={28} />} title="Nenhum pneu cadastrado"
            description="Cadastre cada pneu pelo número de fogo para acompanhar onde ele está, quanto rodou e quanto custou." action={newButton} />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
