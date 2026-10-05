import { Suspense } from 'react';
import { ActionIcon, AppShell, Avatar, Burger, Divider, Group, Menu, NavLink, ScrollArea, Stack, Text, Tooltip, UnstyledButton, useMantineColorScheme } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import {
  IconAlertTriangle,
  IconBellRinging,
  IconReportAnalytics,
  IconMap2,
  IconRouter,
  IconPlugConnected,
  IconSettingsAutomation,
  IconBuildingFactory2,
  IconChecklist,
  IconClipboardList,
  IconFileText,
  IconFiles,
  IconChartDonut,
  IconCircleDot,
  IconCoin,
  IconGasStation,
  IconReceipt,
  IconBuildingStore,
  IconGauge,
  IconListCheck,
  IconBuilding,
  IconBuildingSkyscraper,
  IconChevronDown,
  IconKey,
  IconLayoutDashboard,
  IconLogout,
  IconMoon,
  IconShieldLock,
  IconSteeringWheel,
  IconSun,
  IconTool,
  IconTruck,
  IconTruckLoading,
  IconUsers,
} from '@tabler/icons-react';
import { NavLink as RouterNavLink, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { PERMISSIONS, type Permission } from '../auth/permissions';
import { openChangePassword } from '../features/auth/ChangePasswordModal';
import { NotificationBell } from '../features/alerts/components';
import { GlobalSearch } from '../features/analytics/components';
import { AssistantButton } from '../features/assistant/AssistantPanel';
import { HelpButton } from '../features/help/HelpButton';
import { FormSkeleton } from './EntityFormPage';

interface NavItem {
  label: string;
  to: string;
  icon: typeof IconTruck;
  permission: Permission;
}

const OPERATION: NavItem[] = [
  { label: 'Painel', to: '/', icon: IconLayoutDashboard, permission: PERMISSIONS.dashboard.view },
  { label: 'Alertas', to: '/alertas', icon: IconBellRinging, permission: PERMISSIONS.alerts.view },
  { label: 'Relatórios', to: '/relatorios', icon: IconReportAnalytics, permission: PERMISSIONS.vehicles.view },
  { label: 'Mapa da frota', to: '/mapa', icon: IconMap2, permission: PERMISSIONS.tracking.view },
  { label: 'Veículos', to: '/veiculos', icon: IconTruck, permission: PERMISSIONS.vehicles.view },
  { label: 'Implementos', to: '/implementos', icon: IconTruckLoading, permission: PERMISSIONS.implements.view },
  { label: 'Motoristas', to: '/motoristas', icon: IconSteeringWheel, permission: PERMISSIONS.drivers.view },
  { label: 'Checklists', to: '/checklists', icon: IconChecklist, permission: PERMISSIONS.checklists.view },
  { label: 'Ocorrências', to: '/ocorrencias', icon: IconAlertTriangle, permission: PERMISSIONS.occurrences.view },
  { label: 'Documentos', to: '/documentos', icon: IconFileText, permission: PERMISSIONS.documents.view },
  { label: 'Solicitações de manutenção', to: '/solicitacoes-manutencao', icon: IconClipboardList, permission: PERMISSIONS.maintenance.view },
  { label: 'Ordens de serviço', to: '/ordens-servico', icon: IconTool, permission: PERMISSIONS.maintenance.view },
  { label: 'Combustível', to: '/combustivel', icon: IconGauge, permission: PERMISSIONS.fuel.view },
  { label: 'Abastecimentos', to: '/abastecimentos', icon: IconGasStation, permission: PERMISSIONS.fuel.view },
  { label: 'Painel de pneus', to: '/pneus/painel', icon: IconChartDonut, permission: PERMISSIONS.tires.view },
  { label: 'Pneus', to: '/pneus', icon: IconCircleDot, permission: PERMISSIONS.tires.view },
  { label: 'Financeiro', to: '/financeiro', icon: IconCoin, permission: PERMISSIONS.finance.view },
  { label: 'Despesas', to: '/financeiro/despesas', icon: IconReceipt, permission: PERMISSIONS.finance.view },
];

const SETTINGS: NavItem[] = [
  { label: 'Regras de automação', to: '/configuracoes/automacoes', icon: IconSettingsAutomation, permission: PERMISSIONS.automation.manage },
  { label: 'Rastreadores', to: '/configuracoes/rastreadores', icon: IconRouter, permission: PERMISSIONS.tracking.manage },
  { label: 'Integrações', to: '/configuracoes/integracoes', icon: IconPlugConnected, permission: PERMISSIONS.tracking.manage },
  { label: 'Modelos de checklist', to: '/configuracoes/checklists', icon: IconListCheck, permission: PERMISSIONS.operations.configure },
  { label: 'Tipos de documento', to: '/configuracoes/tipos-de-documento', icon: IconFiles, permission: PERMISSIONS.operations.configure },
  { label: 'Planos de manutenção', to: '/planos-manutencao', icon: IconListCheck, permission: PERMISSIONS.maintenance.manageplans },
  { label: 'Oficinas', to: '/oficinas', icon: IconBuildingFactory2, permission: PERMISSIONS.maintenance.manageworkshops },
  { label: 'Postos de combustível', to: '/postos', icon: IconBuildingStore, permission: PERMISSIONS.fuel.managestations },
  { label: 'Combustível', to: '/configuracoes/combustivel', icon: IconGasStation, permission: PERMISSIONS.fuel.configure },
  { label: 'Pneus', to: '/configuracoes/pneus', icon: IconCircleDot, permission: PERMISSIONS.tires.managesettings },
  { label: 'Categorias de despesa', to: '/financeiro/categorias', icon: IconCoin, permission: PERMISSIONS.finance.managecategories },
  { label: 'Centros de custo', to: '/financeiro/centros-de-custo', icon: IconBuildingFactory2, permission: PERMISSIONS.finance.managecostcenters },
  { label: 'Despesas recorrentes', to: '/financeiro/recorrentes', icon: IconReceipt, permission: PERMISSIONS.finance.managerecurring },
  { label: 'Orçamentos', to: '/financeiro/orcamentos', icon: IconListCheck, permission: PERMISSIONS.finance.managebudgets },
];

const ADMINISTRATION: NavItem[] = [
  { label: 'Usuários', to: '/usuarios', icon: IconUsers, permission: PERMISSIONS.users.view },
  { label: 'Papéis e permissões', to: '/papeis', icon: IconShieldLock, permission: PERMISSIONS.roles.view },
  { label: 'Minha empresa', to: '/minha-empresa', icon: IconBuilding, permission: PERMISSIONS.companies.view },
  { label: 'Empresas', to: '/empresas', icon: IconBuildingSkyscraper, permission: PERMISSIONS.companies.manage },
];

export function AppLayout() {
  const [opened, { toggle, close }] = useDisclosure();
  const { user, can, logout } = useAuth();
  const { colorScheme, toggleColorScheme } = useMantineColorScheme();
  const location = useLocation();

  const renderItems = (items: NavItem[]) =>
    items
      .filter((item) => can(item.permission))
      .map((item) => (
        <NavLink
          key={item.to}
          component={RouterNavLink}
          to={item.to}
          end={item.to === '/'}
          label={item.label}
          leftSection={<item.icon size={20} stroke={1.6} />}
          active={item.to === '/' ? location.pathname === '/' : location.pathname.startsWith(item.to)}
          onClick={close}
          variant="light"
          style={{ borderRadius: 'var(--mantine-radius-md)' }}
        />
      ));

  const administration = renderItems(ADMINISTRATION);
  const settings = renderItems(SETTINGS);
  const initials = user?.name.split(' ').map((p) => p[0]).slice(0, 2).join('').toUpperCase();

  return (
    <AppShell
      header={{ height: 60 }}
      navbar={{ width: 260, breakpoint: 'md', collapsed: { mobile: !opened } }}
      padding={{ base: 'md', sm: 'lg' }}
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group gap="sm" wrap="nowrap">
            <Burger opened={opened} onClick={toggle} hiddenFrom="md" size="sm" aria-label="Abrir menu" />
            <img src="/favicon.svg" alt="Controle de Frota" width={28} height={28} />
            {/* Below 576px the header icons need the room; the logo stays. */}
            <div className="hide-below-xs">
              <Text fw={700} lh={1.1}>
                Controle de Frota
              </Text>
              <Text size="xs" c="dimmed" lh={1.1} visibleFrom="xs">
                {user?.companyName}
              </Text>
            </div>
          </Group>
          <Group gap="xs" wrap="nowrap">
            <GlobalSearch />
            <AssistantButton />
            <NotificationBell />
            <HelpButton />
            <Tooltip label={colorScheme === 'dark' ? 'Tema claro' : 'Tema escuro'}>
              <ActionIcon variant="subtle" color="gray" size="lg" onClick={toggleColorScheme} aria-label="Alternar tema claro/escuro" visibleFrom="sm">
                {colorScheme === 'dark' ? <IconSun size={18} /> : <IconMoon size={18} />}
              </ActionIcon>
            </Tooltip>
            <Menu position="bottom-end" shadow="md" width={240}>
              <Menu.Target>
                <UnstyledButton aria-label="Menu do usuário">
                  <Group gap={8} wrap="nowrap">
                    <Avatar color="brand" radius="xl" size={34}>
                      {initials}
                    </Avatar>
                    <Text size="sm" fw={500} visibleFrom="sm" maw={160} truncate>
                      {user?.name}
                    </Text>
                    <IconChevronDown size={14} />
                  </Group>
                </UnstyledButton>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>
                  <Text size="sm" fw={600} c="var(--mantine-color-text)" truncate>
                    {user?.name}
                  </Text>
                  <Text size="xs" c="dimmed" truncate>
                    {user?.email}
                  </Text>
                </Menu.Label>
                <Menu.Divider />
                <Menu.Item hiddenFrom="sm" leftSection={colorScheme === 'dark' ? <IconSun size={16} /> : <IconMoon size={16} />} onClick={toggleColorScheme}>
                  {colorScheme === 'dark' ? 'Tema claro' : 'Tema escuro'}
                </Menu.Item>
                <Menu.Item leftSection={<IconKey size={16} />} onClick={openChangePassword}>
                  Trocar senha
                </Menu.Item>
                <Menu.Item leftSection={<IconLogout size={16} />} color="red" onClick={() => void logout()}>
                  Sair
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="sm">
        <AppShell.Section grow component={ScrollArea}>
          <Stack gap={4}>
            <Text size="xs" fw={600} c="dimmed" tt="uppercase" px="sm" pt="xs" pb={4}>
              Operação
            </Text>
            {renderItems(OPERATION)}
            {settings.length > 0 && (
              <>
                <Divider my="sm" />
                <Text size="xs" fw={600} c="dimmed" tt="uppercase" px="sm" pb={4}>
                  Configurações
                </Text>
                {settings}
              </>
            )}
            {administration.length > 0 && (
              <>
                <Divider my="sm" />
                <Text size="xs" fw={600} c="dimmed" tt="uppercase" px="sm" pb={4}>
                  Administração
                </Text>
                {administration}
              </>
            )}
          </Stack>
        </AppShell.Section>
        <AppShell.Section>
          <Text size="xs" c="dimmed" px="sm" pt="sm">
            {user?.roles.join(', ')}
          </Text>
        </AppShell.Section>
      </AppShell.Navbar>

      <AppShell.Main bg="var(--app-background)">
        <div style={{ maxWidth: 1280, margin: '0 auto' }}>
          <Suspense fallback={<FormSkeleton />}>
            <Outlet />
          </Suspense>
        </div>
      </AppShell.Main>
    </AppShell>
  );
}
