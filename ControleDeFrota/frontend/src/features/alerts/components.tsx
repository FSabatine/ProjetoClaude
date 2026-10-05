import { ActionIcon, Anchor, Badge, Button, Divider, Group, Indicator, Paper, Popover, ScrollArea, Skeleton, Stack, Text, ThemeIcon, Tooltip, UnstyledButton } from '@mantine/core';
import {
  IconAlertTriangle, IconBell, IconChecks, IconCircleCheck, IconCircleDot, IconCoin, IconFileAlert, IconGasStation, IconInfoCircle,
  IconTool, IconTruck,
} from '@tabler/icons-react';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime } from '../../lib/format';
import { attentionApi, notificationsApi } from './api';
import {
  ALERT_SEVERITY, ALERT_STATUS, type AlertCategory, type AlertSeverity, type AlertStatus, type FleetAlert,
} from './alerts';
import classes from './alerts.module.css';

export const SeverityBadge = ({ value }: { value: AlertSeverity }) => <StatusBadge value={value} map={ALERT_SEVERITY} />;
export const AlertStatusBadge = ({ value }: { value: AlertStatus }) => <StatusBadge value={value} map={ALERT_STATUS} />;

export const CATEGORY_ICON: Record<AlertCategory, typeof IconTruck> = {
  Maintenance: IconTool,
  Fuel: IconGasStation,
  Tires: IconCircleDot,
  Finance: IconCoin,
  Documents: IconFileAlert,
  Operations: IconTruck,
};

const severityColor = (s: AlertSeverity) => ALERT_SEVERITY[s].color;

/** One alert line (dashboard / lists): icon by category, color + text by severity — never color alone. */
export function AlertLine({ alert }: { alert: FleetAlert }) {
  const navigate = useNavigate();
  const Icon = CATEGORY_ICON[alert.category];
  return (
    <UnstyledButton className={classes.row} onClick={() => navigate(`/alertas/${alert.id}`)} aria-label={`${ALERT_SEVERITY[alert.severity].label}: ${alert.title}`}>
      <Group wrap="nowrap" gap="md" align="flex-start">
        <ThemeIcon variant="light" color={severityColor(alert.severity)} size={36} radius="md"><Icon size={20} /></ThemeIcon>
        <div style={{ minWidth: 0, flex: 1 }}>
          <Group gap={6} wrap="nowrap" justify="space-between">
            <Text size="sm" fw={600} lineClamp={1}>{alert.title}</Text>
            <SeverityBadge value={alert.severity} />
          </Group>
          <Text size="sm" c="dimmed" lineClamp={2}>{alert.explanation}</Text>
        </div>
      </Group>
    </UnstyledButton>
  );
}

/**
 * "Requer atenção" (dashboard): what needs someone, most important first; each line opens the records behind it.
 * Alert groups come from the last automatic check; work queues are live.
 */
export function AttentionPanel() {
  const query = attentionApi.useAttention();
  const navigate = useNavigate();
  const data = query.data;

  return (
    <Paper p={{ base: 'md', sm: 'lg' }}>
      <Group justify="space-between" mb="sm" align="flex-start">
        <div>
          <Group gap="xs">
            <Text fw={650}>Requer atenção</Text>
            {data && data.totalCount > 0 && <Badge color="red" variant="filled">{data.totalCount}</Badge>}
          </Group>
          <Text size="xs" c="dimmed">
            {data?.alertsCheckedAt ? `Alertas verificados em ${formatDateTime(data.alertsCheckedAt)} · filas em tempo real` : 'Filas em tempo real · alertas verificados automaticamente a cada hora'}
          </Text>
        </div>
      </Group>
      {query.error && !data && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
      {!data && !query.error && <Stack gap="xs">{[1, 2, 3].map((i) => <Skeleton key={i} height={44} />)}</Stack>}
      {data && data.items.length === 0 && (
        <EmptyState icon={<IconCircleCheck size={28} />} title="Nada pendente agora"
          description="Manutenções atrasadas, consumo fora do padrão, pneus no limite, despesas em atraso e revisões pendentes aparecem aqui." />
      )}
      <Stack gap={6}>
        {data?.items.map((item) => {
          const Icon = item.severity === 'Critical' ? IconAlertTriangle : CATEGORY_ICON[item.category];
          return (
            <UnstyledButton key={item.key} className={classes.row} onClick={() => navigate(item.link)}
              aria-label={`${item.count} ${item.label}. ${ALERT_SEVERITY[item.severity].label}. Ver registros`}>
              <Group wrap="nowrap" gap="sm">
                <ThemeIcon variant="light" color={severityColor(item.severity)} size={32} radius="md"><Icon size={18} /></ThemeIcon>
                <Text fw={700} fz="lg" w={44} ta="right">{item.count.toLocaleString('pt-BR')}</Text>
                <Text size="sm" style={{ flex: 1 }}>{item.label}</Text>
                {item.severity === 'Critical' && <Badge color="red" variant="light">Crítico</Badge>}
              </Group>
            </UnstyledButton>
          );
        })}
      </Stack>
    </Paper>
  );
}

/** Header bell: personal in-app notifications (no e-mail/push in this version). */
export function NotificationBell() {
  const { can } = useAuth();
  const enabled = can(PERMISSIONS.alerts.view);
  const [opened, setOpened] = useState(false);
  const unread = notificationsApi.useUnreadCount(enabled);
  const list = notificationsApi.useList(enabled && opened);
  const markRead = notificationsApi.useMarkRead();
  const markAll = notificationsApi.useMarkAllRead();
  const navigate = useNavigate();
  if (!enabled) return null;

  const count = unread.data ?? 0;
  return (
    <Popover opened={opened} onChange={setOpened} width={360} position="bottom-end" shadow="md" withArrow>
      <Popover.Target>
        <Tooltip label="Notificações" disabled={opened}>
          <Indicator label={count > 99 ? '99+' : count} size={16} disabled={count === 0} color="red" offset={4}>
            <ActionIcon variant="subtle" color="gray" size="lg" onClick={() => setOpened((o) => !o)}
              aria-label={count > 0 ? `Notificações: ${count} não lida(s)` : 'Notificações'}>
              <IconBell size={18} />
            </ActionIcon>
          </Indicator>
        </Tooltip>
      </Popover.Target>
      <Popover.Dropdown p={0}>
        <Group justify="space-between" px="md" py="sm">
          <Text fw={650}>Notificações</Text>
          {count > 0 && (
            <Button size="compact-xs" variant="subtle" leftSection={<IconChecks size={14} />} loading={markAll.isPending}
              onClick={() => markAll.mutate(undefined)}>Marcar todas como lidas</Button>
          )}
        </Group>
        <Divider />
        <ScrollArea.Autosize mah={420}>
          {!list.data && <Stack p="md" gap="xs">{[1, 2, 3].map((i) => <Skeleton key={i} height={48} />)}</Stack>}
          {list.data?.items.length === 0 && (
            <Stack align="center" gap={4} p="lg">
              <IconInfoCircle size={24} color="var(--mantine-color-dimmed)" />
              <Text size="sm" c="dimmed" ta="center">Nenhuma notificação. As regras de automação avisam aqui o que precisa da sua atenção.</Text>
            </Stack>
          )}
          {list.data?.items.map((n) => (
            <UnstyledButton key={n.id} className={classes.notification} data-unread={!n.readAt || undefined}
              onClick={() => {
                if (!n.readAt) markRead.mutate(n.id);
                setOpened(false);
                if (n.link) navigate(n.link);
              }}>
              <Group gap="sm" wrap="nowrap" align="flex-start">
                <ThemeIcon variant="light" color={severityColor(n.severity)} size={28} radius="xl" mt={2}>
                  {n.severity === 'Critical' ? <IconAlertTriangle size={16} /> : <IconBell size={16} />}
                </ThemeIcon>
                <div style={{ minWidth: 0 }}>
                  <Text size="sm" fw={n.readAt ? 500 : 700} lineClamp={2}>{n.title}</Text>
                  <Text size="xs" c="dimmed" lineClamp={2}>{n.message}</Text>
                  <Text size="xs" c="dimmed">{formatDateTime(n.createdAt)}</Text>
                </div>
              </Group>
            </UnstyledButton>
          ))}
        </ScrollArea.Autosize>
        <Divider />
        <Group justify="center" py="xs">
          <Anchor component={Link} to="/alertas" size="sm" onClick={() => setOpened(false)}>Abrir a central de alertas</Anchor>
        </Group>
      </Popover.Dropdown>
    </Popover>
  );
}
