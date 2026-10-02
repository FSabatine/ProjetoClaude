import { Alert, Anchor, Button, Group, Paper, SimpleGrid, Stack, Text } from '@mantine/core';
import {
  IconAlertTriangle, IconChecklist, IconCircleDot, IconFileText, IconGasStation, IconGauge, IconHistory, IconInfoCircle, IconPencil, IconPlayerPlay, IconSteeringWheel, IconTool,
} from '@tabler/icons-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { DetailTabs, HeaderFact, InfoGrid, Section, type DetailTab } from '../../components/DetailLayout';
import { EntityFormPage } from '../../components/EntityFormPage';
import { PageHeader } from '../../components/PageHeader';
import { formatCurrency, formatDate, formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { dueLabel } from '../../lib/mileage';
import { VehicleAssignmentPanel } from '../assignments/AssignmentPanel';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { ChecklistsPanel } from '../checklists/ChecklistsPanel';
import { DocumentsPanel } from '../documents/DocumentsPanel';
import { HistoryTimeline } from '../history/HistoryTimeline';
import { VehicleFuelPanel } from '../fuel/VehicleFuelPanel';
import { AssetTirePanel } from '../tires/AssetTirePanel';
import { VehicleMaintenancePanel } from '../maintenance/VehicleMaintenancePanel';
import { MileagePanel } from '../mileage/MileagePanel';
import { OccurrencesPanel } from '../occurrences/OccurrencesPanel';
import { documentsApi, mileageApi, occurrencesApi } from '../operations/api';
import { VEHICLE_OPERATIONAL_STATUS } from '../operations/labels';
import { VEHICLE_FUEL_TYPE, VEHICLE_CATEGORY, VEHICLE_TYPE, vehiclesApi, type Vehicle } from './vehicles';

const STALE_DAYS = 7;

export function VehicleDetailPage() {
  const { id } = useParams();
  const detail = vehiclesApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(v) => v && <VehicleHub vehicle={v} />}</EntityFormPage>;
}

/** The vehicle hub (UX_UI.md): who drives it, its mileage, documents, inspections, occurrences and history in one place. */
function VehicleHub({ vehicle: v }: { vehicle: Vehicle }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const plate = formatPlate(v.licensePlate);
  const openOccurrences = occurrencesApi.useList({ vehicleId: v.id, openOnly: 'true', pageSize: 1 }, can(PERMISSIONS.occurrences.view));

  const tabs: DetailTab[] = [
    { value: 'visao-geral', label: 'Visão geral', icon: <IconInfoCircle size={16} />, content: <Overview vehicle={v} /> },
    ...(can(PERMISSIONS.assignments.view) ? [{ value: 'motorista', label: 'Motorista', icon: <IconSteeringWheel size={16} />, content: <VehicleAssignmentPanel vehicle={v} /> }] : []),
    { value: 'quilometragem', label: 'Quilometragem', icon: <IconGauge size={16} />, content: <MileagePanel vehicle={v} /> },
    ...(can(PERMISSIONS.documents.view) ? [{ value: 'documentos', label: 'Documentos', icon: <IconFileText size={16} />, content: <DocumentsPanel ownerType="Vehicle" ownerId={v.id} /> }] : []),
    ...(can(PERMISSIONS.checklists.view) ? [{ value: 'checklists', label: 'Checklists', icon: <IconChecklist size={16} />, content: <ChecklistsPanel filter={{ vehicleId: v.id }} vehicleId={v.id} /> }] : []),
    ...(can(PERMISSIONS.occurrences.view) ? [{
      value: 'ocorrencias', label: 'Ocorrências', icon: <IconAlertTriangle size={16} />, count: openOccurrences.data?.totalCount,
      content: <OccurrencesPanel filter={{ vehicleId: v.id }} defaults={{ vehicleId: v.id, vehicleLabel: plate, driverId: v.currentAssignment?.driverId, driverLabel: v.currentAssignment?.driverName }} />,
    }] : []),
    ...(can(PERMISSIONS.maintenance.view) ? [{ value: 'manutencao', label: 'Manutenção', icon: <IconTool size={16} />, content: <VehicleMaintenancePanel vehicleId={v.id} /> }] : []),
    ...(can(PERMISSIONS.fuel.view) ? [{ value: 'combustivel', label: 'Combustível', icon: <IconGasStation size={16} />, content: <VehicleFuelPanel vehicleId={v.id} vehicleInactive={v.status === 'Inactive'} /> }] : []),
    ...(can(PERMISSIONS.tires.view) ? [{ value: 'pneus', label: 'Pneus', icon: <IconCircleDot size={16} />, content: <AssetTirePanel asset={{ kind: 'vehicles', id: v.id }} /> }] : []),
    { value: 'historico', label: 'Histórico', icon: <IconHistory size={16} />, content: <HistoryTimeline owner="vehicles" id={v.id} /> },
  ];

  const primary = can(PERMISSIONS.checklists.execute) && v.status !== 'Inactive' ? (
    <Button leftSection={<IconPlayerPlay size={18} />} onClick={() => navigate(`/checklists/realizar?veiculo=${v.id}`)}>Realizar checklist</Button>
  ) : null;

  return (
    <>
      <PageHeader
        title={`${plate} · ${v.manufacturer} ${v.model}`}
        description={`${VEHICLE_TYPE[v.type].label} · ${v.modelYear}`}
        breadcrumbs={[{ label: 'Veículos', to: '/veiculos' }, { label: plate }]}
        action={
          <Group gap="xs">
            <AuditHistoryButton entity="Vehicle" id={v.id} />
            <Button variant="default" leftSection={<IconPencil size={18} />} onClick={() => navigate(`/veiculos/${v.id}/editar`)}>
              {can(PERMISSIONS.vehicles.update) ? 'Editar' : 'Ver cadastro'}
            </Button>
            {primary}
          </Group>
        }
      />
      <Paper p="md" mb="md">
        <SimpleGrid cols={{ base: 2, md: 4 }} spacing="md">
          <HeaderFact label="Situação" value={<StatusBadge value={v.operationalStatus} map={VEHICLE_OPERATIONAL_STATUS} />} />
          <HeaderFact label="Placa" value={<Text ff="monospace" fw={700} size="lg">{plate}</Text>} />
          <HeaderFact label="Hodômetro" value={`${formatNumber(v.currentOdometerKm)} km`}
            hint={v.odometerUpdatedAt ? `em ${formatDateTime(v.odometerUpdatedAt)}` : 'sem leitura registrada'} />
          <HeaderFact label="Motorista" value={v.currentAssignment ? v.currentAssignment.driverName : 'Sem motorista'}
            hint={v.currentAssignment && `desde ${formatDate(v.currentAssignment.startedAt)}`} />
        </SimpleGrid>
      </Paper>
      <DetailTabs tabs={tabs} />
    </>
  );
}

function Overview({ vehicle: v }: { vehicle: Vehicle }) {
  const { can } = useAuth();
  const documents = documentsApi.useList({ vehicleId: v.id, alertsOnly: 'true', pageSize: 5 }, can(PERMISSIONS.documents.view));
  const occurrences = occurrencesApi.useList({ vehicleId: v.id, openOnly: 'true', pageSize: 5 }, can(PERMISSIONS.occurrences.view));
  const pendingReadings = mileageApi.useReadings(v.id, { status: 'PendingReview', pageSize: 1 });
  const stale = v.status !== 'Inactive' && (!v.odometerUpdatedAt || Date.now() - new Date(v.odometerUpdatedAt).getTime() > STALE_DAYS * 86_400_000);

  const alerts = [
    ...(documents.data?.items ?? []).map((d) => ({
      key: d.id, color: d.status === 'Expired' ? 'red' : 'orange',
      text: `${d.documentTypeName} ${dueLabel(d.daysUntilExpiration)}.`, to: '?aba=documentos',
    })),
    ...(occurrences.data?.items ?? []).filter((o) => o.severity === 'Critical' || o.severity === 'High').map((o) => ({
      key: o.id, color: o.severity === 'Critical' ? 'red' : 'orange', text: `Ocorrência em aberto: ${o.description}`, to: `/ocorrencias/${o.id}`,
    })),
    ...(pendingReadings.data && pendingReadings.data.totalCount > 0
      ? [{ key: 'km', color: 'orange', text: 'Há leitura de hodômetro suspeita aguardando revisão.', to: '?aba=quilometragem' }] : []),
    ...(stale ? [{ key: 'stale', color: 'yellow', text: `Hodômetro sem atualização há mais de ${STALE_DAYS} dias.`, to: '?aba=quilometragem' }] : []),
  ];

  return (
    <Stack gap="md">
      {alerts.length > 0 && (
        <Stack gap="xs">
          {alerts.map((a) => (
            <Alert key={a.key} color={a.color} icon={<IconAlertTriangle size={18} />} p="xs">
              <Anchor component={Link} to={a.to} size="sm" c="inherit">{a.text}</Anchor>
            </Alert>
          ))}
        </Stack>
      )}
      {alerts.length === 0 && <Alert color="teal" icon={<IconInfoCircle />} p="xs">Nenhum alerta para este veículo.</Alert>}
      <Section title="Dados gerais">
        <InfoGrid items={[
          { label: 'Fabricante / modelo', value: `${v.manufacturer} ${v.model}` },
          { label: 'Ano', value: `${v.manufacturingYear}/${v.modelYear}` },
          { label: 'Tipo', value: VEHICLE_TYPE[v.type].label },
          { label: 'Categoria', value: v.category && VEHICLE_CATEGORY[v.category].label },
          { label: 'Combustível', value: VEHICLE_FUEL_TYPE[v.fuelType].label },
          { label: 'Cor', value: v.color },
          { label: 'RENAVAM', value: v.renavam },
          { label: 'Chassi', value: v.chassis },
          { label: 'Capacidade de carga', value: v.cargoCapacityKg !== null ? `${formatNumber(v.cargoCapacityKg)} kg` : null },
          { label: 'Horímetro', value: v.hourMeter !== null ? `${formatNumber(v.hourMeter)} h` : null },
          { label: 'Tanque', value: v.fuelTankCapacity !== null ? `${formatNumber(v.fuelTankCapacity)}${v.secondaryFuelTankCapacity ? ` + ${formatNumber(v.secondaryFuelTankCapacity)}` : ''} L` : null },
          { label: 'Consumo esperado', value: v.expectedConsumption !== null ? `${formatNumber(v.expectedConsumption)} km/L` : null },
          { label: 'Aquisição', value: v.acquisitionDate ? `${formatDate(v.acquisitionDate)} · ${formatCurrency(v.acquisitionValue)}` : null },
        ]} />
        {v.notes && <Text size="sm" c="dimmed" style={{ whiteSpace: 'pre-wrap' }}>{v.notes}</Text>}
      </Section>
    </Stack>
  );
}
