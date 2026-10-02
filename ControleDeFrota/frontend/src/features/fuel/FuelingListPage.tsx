import { Badge, Button, Group, NumberInput, Paper, Popover, Select, SimpleGrid, Stack, Text } from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconAdjustmentsHorizontal, IconGasStation, IconPlus } from '@tabler/icons-react';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, StatusBadge, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { DriverPicker, VehiclePicker } from '../../components/EntityPickers';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatCurrency, formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { formatConsumption, formatQuantity, formatUnitPrice } from '../../lib/fuel';
import { fuelStationsApi, fuelTypesApi, fuelingsApi } from './api';
import { FUEL_ANOMALY_TYPE, FUELING_STATUS, PAYMENT_METHOD, type FuelingListItem } from './fuel';

const FILTERS = [
  'status', 'anomalyType', 'paymentMethod', 'fuelTypeId', 'fuelStationId', 'vehicleId', 'driverId',
  'from', 'to', 'minQuantity', 'maxQuantity', 'minUnitPrice', 'maxUnitPrice',
] as const;

export function FuelingListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'fueledAt', direction: 'Desc' });
  const query = fuelingsApi.useList(list.apiParams);
  const types = fuelTypesApi.useList(true);
  const stations = fuelStationsApi.useList({ pageSize: 100, sortBy: 'name' });
  const canSeeCosts = can(PERMISSIONS.fuel.viewcosts);
  const f = list.filters;

  const newButton = can(PERMISSIONS.fuel.create) && (
    <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/abastecimentos/novo')}>Registrar abastecimento</Button>
  );

  // Prices of other people's fuelings come back empty without the cost permission: the columns show only when useful.
  const columns: Column<FuelingListItem>[] = [
    { key: 'fueledAt', header: 'Data', sortKey: 'fueledAt', render: (r) => formatDateTime(r.fueledAt) },
    { key: 'vehicle', header: 'Veículo', sortKey: 'licensePlate', render: (r) => <Text size="sm" fw={600} ff="monospace">{formatPlate(r.licensePlate)}</Text> },
    { key: 'fuel', header: 'Combustível', secondary: true, render: (r) => r.fuelTypeName },
    { key: 'quantity', header: 'Quantidade', sortKey: 'quantity', align: 'right', render: (r) => formatQuantity(r.quantity, r.unit) },
    ...(canSeeCosts ? [
      { key: 'price', header: 'Preço', align: 'right' as const, secondary: true, render: (r: FuelingListItem) => formatUnitPrice(r.unitPrice, r.unit) },
      { key: 'total', header: 'Total', align: 'right' as const, render: (r: FuelingListItem) => formatCurrency(r.totalAmount) },
    ] : []),
    { key: 'odometer', header: 'Hodômetro', sortKey: 'odometerKm', align: 'right', render: (r) => `${formatNumber(r.odometerKm)} km` },
    { key: 'consumption', header: 'Consumo', align: 'right', secondary: true,
      render: (r) => r.isFullTank ? formatConsumption(r.consumption, r.unit) : <Text size="xs" c="dimmed">parcial</Text> },
    { key: 'status', header: 'Situação', sortKey: 'status', render: (r) => <StatusBadge value={r.status} map={FUELING_STATUS} /> },
  ];

  const moreFilters = [f.minQuantity, f.maxQuantity, f.minUnitPrice, f.maxUnitPrice, f.paymentMethod, f.driverId].filter(Boolean).length;

  return (
    <>
      <PageHeader title="Abastecimentos" description="Registro de todos os abastecimentos da frota, com consumo e alertas." action={newButton} />
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por placa, motorista, posto ou cupom"
        filters={[
          { key: 'status', placeholder: 'Situação', data: toSelectData(FUELING_STATUS), value: f.status, onChange: (v) => list.setFilter('status', v) },
          { key: 'anomalyType', placeholder: 'Alerta', data: toSelectData(FUEL_ANOMALY_TYPE), value: f.anomalyType, onChange: (v) => list.setFilter('anomalyType', v) },
          { key: 'fuelTypeId', placeholder: 'Combustível', data: (types.data ?? []).map((t) => ({ value: t.id, label: t.name })), value: f.fuelTypeId, onChange: (v) => list.setFilter('fuelTypeId', v) },
          { key: 'fuelStationId', placeholder: 'Posto', data: (stations.data?.items ?? []).map((s) => ({ value: s.id, label: s.name })), value: f.fuelStationId, onChange: (v) => list.setFilter('fuelStationId', v) },
        ]}
      />
      <Paper p="sm" mb="md" mt={-8}>
        <Group gap="sm" wrap="wrap">
          <VehiclePicker aria-label="Veículo" placeholder="Veículo" value={f.vehicleId} onChange={(v) => list.setFilter('vehicleId', v)} style={{ flex: '1 1 220px' }} />
          <DatePickerInput type="range" aria-label="Período" placeholder="Período" valueFormat="DD/MM/YYYY" clearable maxDate={new Date()}
            value={[f.from ? dayjs(f.from).toDate() : null, f.to ? dayjs(f.to).toDate() : null]}
            onChange={([start, end]) => {
              list.setFilter('from', start ? dayjs(start).format('YYYY-MM-DD') : null);
              if (!start || end) list.setFilter('to', end ? dayjs(end).format('YYYY-MM-DD') : null);
            }}
            style={{ flex: '1 1 220px', maxWidth: 280 }} />
          <Popover width={320} position="bottom-end" shadow="md">
            <Popover.Target>
              <Button variant="default" leftSection={<IconAdjustmentsHorizontal size={16} />}
                rightSection={moreFilters > 0 ? <Badge size="sm" circle>{moreFilters}</Badge> : undefined}>Mais filtros</Button>
            </Popover.Target>
            <Popover.Dropdown>
              <Stack gap="sm">
                <DriverPicker label="Motorista" excludeInactive={false} value={f.driverId} onChange={(v) => list.setFilter('driverId', v)} />
                <Select label="Forma de pagamento" data={toSelectData(PAYMENT_METHOD)} clearable allowDeselect value={f.paymentMethod}
                  onChange={(v) => list.setFilter('paymentMethod', v)} />
                <SimpleGrid cols={2}>
                  <NumberInput label="Quantidade mín." decimalSeparator="," allowNegative={false} value={f.minQuantity ?? ''}
                    onChange={(v) => list.setFilter('minQuantity', v === '' ? null : String(v))} />
                  <NumberInput label="Quantidade máx." decimalSeparator="," allowNegative={false} value={f.maxQuantity ?? ''}
                    onChange={(v) => list.setFilter('maxQuantity', v === '' ? null : String(v))} />
                  {canSeeCosts && (
                    <>
                      <NumberInput label="Preço mín." prefix="R$ " decimalSeparator="," decimalScale={3} allowNegative={false} value={f.minUnitPrice ?? ''}
                        onChange={(v) => list.setFilter('minUnitPrice', v === '' ? null : String(v))} />
                      <NumberInput label="Preço máx." prefix="R$ " decimalSeparator="," decimalScale={3} allowNegative={false} value={f.maxUnitPrice ?? ''}
                        onChange={(v) => list.setFilter('maxUnitPrice', v === '' ? null : String(v))} />
                    </>
                  )}
                </SimpleGrid>
              </Stack>
            </Popover.Dropdown>
          </Popover>
        </Group>
      </Paper>
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(r) => r.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(r) => navigate(`/abastecimentos/${r.id}`)}
        renderCard={(r) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={700} ff="monospace">{formatPlate(r.licensePlate)}</Text>
              <StatusBadge value={r.status} map={FUELING_STATUS} />
            </Group>
            <Text size="sm">{formatQuantity(r.quantity, r.unit)} de {r.fuelTypeName}{r.totalAmount !== null ? ` · ${formatCurrency(r.totalAmount)}` : ''}</Text>
            <Text size="xs" c="dimmed">{formatDateTime(r.fueledAt)} · {formatNumber(r.odometerKm)} km{r.fuelStationName ? ` · ${r.fuelStationName}` : ''}</Text>
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconGasStation size={28} />} title="Nenhum abastecimento registrado"
            description="Registre os abastecimentos para acompanhar o consumo, os custos e os alertas de cada veículo." action={newButton} />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
