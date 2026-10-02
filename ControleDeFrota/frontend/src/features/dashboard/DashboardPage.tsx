import type { ReactNode } from 'react';
import { Anchor, Badge, Group, Paper, SimpleGrid, Skeleton, Stack, Text, ThemeIcon, Title, UnstyledButton } from '@mantine/core';
import {
  IconAlertTriangle, IconChecklist, IconCircleCheck, IconClock, IconFileAlert, IconFileText, IconGasStation,
  IconGauge, IconHourglass, IconId, IconProgress,
  IconRoute, IconSteeringWheel, IconTool, IconTruck, IconTruckDelivery, IconBan, IconArchive,
} from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { formatNumber, formatPlate } from '../../lib/format';
import classes from './DashboardPage.module.css';

type AlertType =
  | 'LicenseExpired' | 'LicenseExpiringSoon' | 'DocumentExpired' | 'DocumentExpiringSoon' | 'CriticalOccurrence' | 'MileagePendingReview'
  | 'CriticalWorkOrder' | 'FuelingPendingReview';
type EntityType = 'Driver' | 'Vehicle' | 'Implement' | 'Company' | 'Occurrence' | 'WorkOrder' | 'Fueling';

interface DashboardAlert {
  type: AlertType;
  severity: 'Warning' | 'Critical';
  title: string;
  description: string;
  entityType: EntityType;
  entityId: string;
  dueDate: string;
  tab: string | null;
}

interface DashboardData {
  indicators: { activeVehicles: number; availableVehicles: number; vehiclesUnderMaintenance: number; activeDrivers: number };
  fleet: { total: number; available: number; assigned: number; onTrip: number; unavailable: number; underMaintenance: number; inactive: number };
  operations: {
    documentsExpiringSoon: number | null;
    documentsExpired: number | null;
    pendingChecklists: number | null;
    openOccurrences: number | null;
    criticalOpenOccurrences: number | null;
    pendingMileageReviews: number | null;
    vehiclesWithoutRecentMileage: number;
  };
  mileage: {
    fleetKmThisMonth: number;
    averageKmPerVehicleThisMonth: number;
    highestMileageVehicle: { id: string; licensePlate: string; odometerKm: number } | null;
    vehiclesWithoutRecentMileage: number;
    staleAfterDays: number;
  };
  maintenance: {
    dueToday: number;
    dueSoon: number;
    overdue: number;
    inProgress: number;
    waitingParts: number;
    completedThisMonth: number;
    vehiclesUnderMaintenance: number;
  } | null;
  alerts: DashboardAlert[];
  totalAlerts: number;
}

/** Tabs of the hub pages use pt-BR names in the URL. */
const TAB_ROUTE: Record<string, string> = { documents: 'documentos', mileage: 'quilometragem' };

const alertLink = (a: DashboardAlert) => {
  const tab = a.tab ? `?aba=${TAB_ROUTE[a.tab] ?? a.tab}` : '';
  switch (a.entityType) {
    case 'Driver': return `/motoristas/${a.entityId}${tab}`;
    case 'Vehicle': return `/veiculos/${a.entityId}${tab}`;
    case 'Implement': return `/implementos/${a.entityId}`;
    case 'Occurrence': return `/ocorrencias/${a.entityId}`;
    case 'WorkOrder': return `/ordens-servico/${a.entityId}`;
    case 'Fueling': return `/abastecimentos/${a.entityId}`;
    default: return '/minha-empresa';
  }
};

const ALERT_ICON: Record<AlertType, typeof IconId> = {
  LicenseExpired: IconId,
  LicenseExpiringSoon: IconId,
  DocumentExpired: IconFileAlert,
  DocumentExpiringSoon: IconFileText,
  CriticalOccurrence: IconAlertTriangle,
  MileagePendingReview: IconGauge,
  CriticalWorkOrder: IconTool,
  FuelingPendingReview: IconGasStation,
};

/** A number that leads to the filtered list behind it (one click from the KPI to the records). */
function Stat({ label, value, icon: Icon, color, hint, to }: { label: string; value: number; icon: typeof IconTruck; color: string; hint?: string; to: string }) {
  const navigate = useNavigate();
  return (
    <UnstyledButton onClick={() => navigate(to)} className={classes.card} aria-label={`${label}: ${value}. Ver lista`}>
      <Paper p="md" h="100%">
        <Group justify="space-between" align="flex-start" wrap="nowrap" gap="xs">
          <div>
            <Text size="xs" c="dimmed" fw={500}>{label}</Text>
            <Text fz={28} fw={700} lh={1.2}>{value.toLocaleString('pt-BR')}</Text>
            {hint && <Text size="xs" c="dimmed">{hint}</Text>}
          </div>
          <ThemeIcon size={36} radius="md" variant="light" color={color}><Icon size={20} stroke={1.6} /></ThemeIcon>
        </Group>
      </Paper>
    </UnstyledButton>
  );
}

function Block({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <Stack gap="sm">
      <Group justify="space-between">
        <Title order={4} fz="md">{title}</Title>
        {action}
      </Group>
      {children}
    </Stack>
  );
}

export function DashboardPage() {
  const { user, can } = useAuth();
  const navigate = useNavigate();
  const query = useQuery({ queryKey: ['dashboard'], queryFn: () => api.get<DashboardData>('/dashboard').then((r) => r.data) });
  const firstName = user?.name.split(' ')[0];
  const d = query.data;

  if (query.error && !d) return <><PageHeader title={`Olá, ${firstName}`} /><Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper></>;

  const loading = (n: number, h = 96) => <SimpleGrid cols={{ base: 2, md: 4 }}>{Array.from({ length: n }, (_, i) => <Skeleton key={i} height={h} radius="md" />)}</SimpleGrid>;
  const ops = d?.operations;

  const operations = d && [
    ops!.documentsExpired !== null && { label: 'Documentos vencidos', value: ops!.documentsExpired, icon: IconFileAlert, color: 'red', to: '/documentos?status=Expired' },
    ops!.documentsExpiringSoon !== null && { label: 'Documentos vencendo', value: ops!.documentsExpiringSoon, icon: IconFileText, color: 'orange', to: '/documentos?status=ExpiringSoon' },
    ops!.pendingChecklists !== null && { label: 'Checklists pendentes hoje', value: ops!.pendingChecklists, icon: IconChecklist, color: 'grape', to: '/checklists' },
    ops!.openOccurrences !== null && {
      label: 'Ocorrências em aberto', value: ops!.openOccurrences, icon: IconAlertTriangle, color: 'red',
      hint: ops!.criticalOpenOccurrences ? `${ops!.criticalOpenOccurrences} crítica(s)` : undefined, to: '/ocorrencias?openOnly=true',
    },
    { label: 'Sem leitura recente de km', value: ops!.vehiclesWithoutRecentMileage, icon: IconGauge, color: 'yellow',
      hint: `há mais de ${d.mileage.staleAfterDays} dias`, to: '/veiculos?staleMileage=true' },
  ].filter(Boolean) as Parameters<typeof Stat>[0][];

  return (
    <>
      <PageHeader title={`Olá, ${firstName}`} description="Situação operacional da frota agora." />
      <Stack gap="xl">
        <Block title={d ? `Frota (${d.fleet.total} veículos)` : 'Frota'}>
          {!d ? loading(7) : (
            <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }} spacing="sm">
              <Stat label="Disponíveis" value={d.fleet.available} icon={IconCircleCheck} color="teal" hint="sem motorista" to="/veiculos?operationalStatus=Available" />
              <Stat label="Alocados" value={d.fleet.assigned} icon={IconSteeringWheel} color="cyan" hint="com motorista" to="/veiculos?operationalStatus=Assigned" />
              <Stat label="Em viagem" value={d.fleet.onTrip} icon={IconRoute} color="blue" to="/veiculos?operationalStatus=OnTrip" />
              <Stat label="Indisponíveis" value={d.fleet.unavailable} icon={IconBan} color="red" to="/veiculos?operationalStatus=Unavailable" />
              <Stat label="Em manutenção" value={d.fleet.underMaintenance} icon={IconTool} color="orange" to="/veiculos?operationalStatus=UnderMaintenance" />
              <Stat label="Inativos" value={d.fleet.inactive} icon={IconArchive} color="gray" to="/veiculos?operationalStatus=Inactive" />
            </SimpleGrid>
          )}
        </Block>

        {can(PERMISSIONS.maintenance.view) && (
          <Block title="Manutenção">
            {!d ? loading(7) : d.maintenance === null ? null : (
              <SimpleGrid cols={{ base: 2, sm: 3, lg: 7 }} spacing="sm">
                <Stat label="Vence hoje" value={d.maintenance.dueToday} icon={IconClock} color="yellow" to="/ordens-servico" />
                <Stat label="Vencendo" value={d.maintenance.dueSoon} icon={IconHourglass} color="orange" to="/ordens-servico" />
                <Stat label="Atrasadas" value={d.maintenance.overdue} icon={IconAlertTriangle} color="red" to="/ordens-servico" />
                <Stat label="Em andamento" value={d.maintenance.inProgress} icon={IconProgress} color="blue" to="/ordens-servico?status=InProgress" />
                <Stat label="Aguardando peças" value={d.maintenance.waitingParts} icon={IconTool} color="grape" to="/ordens-servico?status=WaitingParts" />
                <Stat label="Concluídas no mês" value={d.maintenance.completedThisMonth} icon={IconCircleCheck} color="teal" to="/ordens-servico?status=Completed" />
                <Stat label="Veículos em manutenção" value={d.maintenance.vehiclesUnderMaintenance} icon={IconTruck} color="gray" to="/veiculos?operationalStatus=UnderMaintenance" />
              </SimpleGrid>
            )}
          </Block>
        )}

        <SimpleGrid cols={{ base: 1, lg: 3 }} spacing="lg">
          <div style={{ gridColumn: 'span 2' }}>
            <Block title="Atenção">
              {!operations ? loading(4) : (
                <SimpleGrid cols={{ base: 2, md: 3 }} spacing="sm">
                  {operations.map((s) => <Stat key={s.label} {...s} />)}
                </SimpleGrid>
              )}
            </Block>
          </div>
          <Block title="Quilometragem do mês">
            {!d ? <Skeleton height={180} /> : (
              <Paper p="md">
                <Stack gap="sm">
                  <div>
                    <Text size="xs" c="dimmed">Rodados pela frota</Text>
                    <Text fz={28} fw={700}>{formatNumber(d.mileage.fleetKmThisMonth)} km</Text>
                  </div>
                  <Group grow>
                    <div>
                      <Text size="xs" c="dimmed">Média por veículo</Text>
                      <Text fw={600}>{formatNumber(d.mileage.averageKmPerVehicleThisMonth)} km</Text>
                    </div>
                    <div>
                      <Text size="xs" c="dimmed">Maior hodômetro</Text>
                      {d.mileage.highestMileageVehicle ? (
                        <Anchor component={Link} to={`/veiculos/${d.mileage.highestMileageVehicle.id}?aba=quilometragem`} fw={600} size="sm">
                          {formatPlate(d.mileage.highestMileageVehicle.licensePlate)} · {formatNumber(d.mileage.highestMileageVehicle.odometerKm)} km
                        </Anchor>
                      ) : <Text>—</Text>}
                    </div>
                  </Group>
                </Stack>
              </Paper>
            )}
          </Block>
        </SimpleGrid>

        <Paper p={{ base: 'md', sm: 'lg' }}>
          <Group justify="space-between" mb="md">
            <Group gap="xs">
              <Title order={4} fz="md">Alertas</Title>
              {d && d.totalAlerts > 0 && <Badge color="red">{d.totalAlerts}</Badge>}
            </Group>
            {d && d.totalAlerts > d.alerts.length && can(PERMISSIONS.documents.view) && (
              <Anchor component={Link} to="/documentos?alertsOnly=true" size="sm">Ver documentos em alerta</Anchor>
            )}
          </Group>
          {!d && <Stack>{[1, 2, 3].map((i) => <Skeleton key={i} height={56} />)}</Stack>}
          {d?.alerts.length === 0 && (
            <EmptyState icon={<IconCircleCheck size={28} />} title="Nenhum alerta no momento"
              description="Documentos e CNHs vencendo, ocorrências críticas e leituras de hodômetro suspeitas aparecem aqui." />
          )}
          <Stack gap="xs">
            {d?.alerts.map((alert) => {
              const Icon = alert.severity === 'Critical' && alert.type !== 'CriticalOccurrence' ? IconAlertTriangle : ALERT_ICON[alert.type];
              return (
                <UnstyledButton key={`${alert.type}-${alert.entityId}-${alert.dueDate}-${alert.description}`} className={classes.alert} onClick={() => navigate(alertLink(alert))}>
                  <Group wrap="nowrap" gap="md">
                    <ThemeIcon variant="light" color={alert.severity === 'Critical' ? 'red' : 'orange'} size={36} radius="md"><Icon size={20} /></ThemeIcon>
                    <div style={{ minWidth: 0 }}>
                      <Text size="sm" fw={600}>{alert.title}</Text>
                      <Text size="sm" c="dimmed">{alert.description}</Text>
                    </div>
                  </Group>
                </UnstyledButton>
              );
            })}
          </Stack>
        </Paper>

        {d && d.fleet.total === 0 && can(PERMISSIONS.vehicles.create) && (
          <Paper p="lg">
            <EmptyState icon={<IconTruckDelivery size={28} />} title="Sua frota ainda está vazia"
              description="Cadastre veículos, implementos e motoristas para acompanhar a frota por aqui."
              action={<Anchor component={Link} to="/veiculos/novo">Cadastrar o primeiro veículo</Anchor>} />
          </Paper>
        )}
      </Stack>
    </>
  );
}
