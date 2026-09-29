import { Anchor, Badge, Group, Paper, SimpleGrid, Skeleton, Stack, Text, ThemeIcon, Title, UnstyledButton } from '@mantine/core';
import { IconAlertTriangle, IconCircleCheck, IconId, IconSteeringWheel, IconTool, IconTruck, IconTruckDelivery } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import classes from './DashboardPage.module.css';

interface DashboardAlert {
  type: 'LicenseExpired' | 'LicenseExpiringSoon';
  severity: 'Warning' | 'Critical';
  title: string;
  description: string;
  entityType: 'Driver';
  entityId: string;
  dueDate: string;
}

interface DashboardData {
  indicators: { activeVehicles: number; availableVehicles: number; vehiclesUnderMaintenance: number; activeDrivers: number };
  alerts: DashboardAlert[];
  totalAlerts: number;
}

interface Indicator {
  label: string;
  value: number;
  icon: typeof IconTruck;
  color: string;
  hint: string;
  /** Drill-down to the list already filtered — one click from the number to the records. */
  to: string;
}

const ENTITY_ROUTES: Record<DashboardAlert['entityType'], string> = { Driver: '/motoristas' };

export function DashboardPage() {
  const { user, can } = useAuth();
  const navigate = useNavigate();
  const query = useQuery({ queryKey: ['dashboard'], queryFn: () => api.get<DashboardData>('/dashboard').then((r) => r.data) });
  const firstName = user?.name.split(' ')[0];

  const indicators: Indicator[] = query.data
    ? [
        { label: 'Veículos ativos', value: query.data.indicators.activeVehicles, icon: IconTruck, color: 'blue', hint: 'Todos exceto inativos', to: '/veiculos' },
        { label: 'Veículos disponíveis', value: query.data.indicators.availableVehicles, icon: IconCircleCheck, color: 'teal', hint: 'Prontos para uso', to: '/veiculos?status=Available' },
        { label: 'Em manutenção', value: query.data.indicators.vehiclesUnderMaintenance, icon: IconTool, color: 'orange', hint: 'Indisponíveis no momento', to: '/veiculos?status=UnderMaintenance' },
        { label: 'Motoristas ativos', value: query.data.indicators.activeDrivers, icon: IconSteeringWheel, color: 'grape', hint: 'Exceto afastados e desligados', to: '/motoristas?status=Active' },
      ]
    : [];

  return (
    <>
      <PageHeader title={`Olá, ${firstName}`} description="Visão geral da frota de hoje." />

      {query.error && !query.data ? (
        <Paper>
          <ErrorState error={query.error} onRetry={() => void query.refetch()} />
        </Paper>
      ) : (
        <Stack gap="lg">
          <SimpleGrid cols={{ base: 1, xs: 2, lg: 4 }} spacing="md">
            {query.isLoading
              ? [1, 2, 3, 4].map((i) => <Skeleton key={i} height={112} radius="md" />)
              : indicators.map((indicator) => (
                  <UnstyledButton key={indicator.label} onClick={() => navigate(indicator.to)} className={classes.card} aria-label={`${indicator.label}: ${indicator.value}. Ver lista`}>
                    <Paper p="lg" h="100%">
                      <Group justify="space-between" align="flex-start" wrap="nowrap">
                        <div>
                          <Text size="sm" c="dimmed" fw={500}>{indicator.label}</Text>
                          <Text fz={34} fw={700} lh={1.2} mt={4}>{indicator.value.toLocaleString('pt-BR')}</Text>
                          <Text size="xs" c="dimmed">{indicator.hint}</Text>
                        </div>
                        <ThemeIcon size={44} radius="md" variant="light" color={indicator.color}>
                          <indicator.icon size={24} stroke={1.6} />
                        </ThemeIcon>
                      </Group>
                    </Paper>
                  </UnstyledButton>
                ))}
          </SimpleGrid>

          {can(PERMISSIONS.drivers.view) && (
            <Paper p={{ base: 'md', sm: 'lg' }}>
              <Group justify="space-between" mb="md">
                <Group gap="xs">
                  <Title order={4}>Alertas recentes</Title>
                  {query.data && query.data.totalAlerts > 0 && <Badge color="red">{query.data.totalAlerts}</Badge>}
                </Group>
                {query.data && query.data.totalAlerts > query.data.alerts.length && (
                  <Anchor component={Link} to="/motoristas?sortBy=licenseExpiresOn&sortDirection=Asc" size="sm">
                    Ver todos
                  </Anchor>
                )}
              </Group>
              {query.isLoading && <Stack>{[1, 2, 3].map((i) => <Skeleton key={i} height={56} />)}</Stack>}
              {query.data?.alerts.length === 0 && (
                <EmptyState
                  icon={<IconCircleCheck size={28} />}
                  title="Nenhum alerta no momento"
                  description="Quando houver CNH vencida ou vencendo nos próximos 30 dias, ela aparecerá aqui."
                />
              )}
              <Stack gap="xs">
                {query.data?.alerts.map((alert) => (
                  <UnstyledButton
                    key={`${alert.type}-${alert.entityId}`}
                    className={classes.alert}
                    onClick={() => navigate(`${ENTITY_ROUTES[alert.entityType]}/${alert.entityId}`)}
                  >
                    <Group wrap="nowrap" gap="md">
                      <ThemeIcon variant="light" color={alert.severity === 'Critical' ? 'red' : 'orange'} size={36} radius="md">
                        {alert.severity === 'Critical' ? <IconAlertTriangle size={20} /> : <IconId size={20} />}
                      </ThemeIcon>
                      <div style={{ minWidth: 0 }}>
                        <Text size="sm" fw={600}>{alert.title}</Text>
                        <Text size="sm" c="dimmed">{alert.description}</Text>
                      </div>
                    </Group>
                  </UnstyledButton>
                ))}
              </Stack>
            </Paper>
          )}

          {query.data && query.data.indicators.activeVehicles === 0 && can(PERMISSIONS.vehicles.create) && (
            <Paper p="lg">
              <EmptyState
                icon={<IconTruckDelivery size={28} />}
                title="Sua frota ainda está vazia"
                description="Cadastre veículos, implementos e motoristas para acompanhar a frota por aqui."
                action={<Anchor component={Link} to="/veiculos/novo">Cadastrar o primeiro veículo</Anchor>}
              />
            </Paper>
          )}
        </Stack>
      )}
    </>
  );
}
