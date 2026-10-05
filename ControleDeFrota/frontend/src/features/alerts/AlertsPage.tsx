import { Badge, Button, Checkbox, Group, Paper, SimpleGrid, Stack, Text } from '@mantine/core';
import { IconBellRinging, IconSettingsAutomation } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { ListToolbar, toSelectData } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { VehiclePicker } from '../../components/EntityPickers';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { useListParams } from '../../hooks/useListParams';
import { formatDateTime, formatPlate } from '../../lib/format';
import { InfoHint } from '../finance/components';
import { alertsApi } from './api';
import { ALERT_CATEGORY, ALERT_SEVERITY, ALERT_STATUS, TRIGGER_LABEL, type FleetAlert } from './alerts';
import { AlertStatusBadge, SeverityBadge } from './components';

const FILTERS = ['status', 'category', 'severity', 'trigger', 'ruleId', 'vehicleId', 'includeClosed', 'assignedToMe'] as const;

export function AlertsPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'priority', direction: 'Desc' });
  const query = alertsApi.useList(list.apiParams);
  const summary = alertsApi.useSummary();
  const f = list.filters;

  const rulesButton = can(PERMISSIONS.automation.manage) && (
    <Button variant="default" leftSection={<IconSettingsAutomation size={18} />} onClick={() => navigate('/configuracoes/automacoes')}>Regras de automação</Button>
  );

  const columns: Column<FleetAlert>[] = [
    { key: 'severity', header: 'Gravidade', sortKey: 'severity', width: 120, render: (a) => <SeverityBadge value={a.severity} /> },
    { key: 'title', header: 'Alerta', render: (a) => (
      <Stack gap={0}>
        <Text size="sm" fw={a.status === 'New' ? 700 : 500}>{a.title}</Text>
        <Text size="xs" c="dimmed" lineClamp={1}>{a.explanation}</Text>
      </Stack>
    ) },
    { key: 'category', header: 'Área', secondary: true, render: (a) => ALERT_CATEGORY[a.category].label },
    { key: 'vehicle', header: 'Veículo', secondary: true, render: (a) => (a.licensePlate ? formatPlate(a.licensePlate) : '—') },
    { key: 'detectedAt', header: 'Detectado em', sortKey: 'detectedAt', secondary: true, render: (a) => formatDateTime(a.detectedAt) },
    { key: 'status', header: 'Situação', render: (a) => (
      <Group gap={4} wrap="nowrap">
        <AlertStatusBadge value={a.status} />
        {a.recurrenceCount > 0 && <Badge variant="outline" color="grape">recorrente</Badge>}
      </Group>
    ) },
  ];

  const s = summary.data;
  return (
    <>
      <PageHeader title="Alertas" description="O que precisa de atenção na frota, do mais importante para o menos. Cada alerta explica o motivo e os números que o geraram." action={rulesButton} />
      {s && (
        <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="sm" mb="md">
          <Paper p="md"><Text size="xs" c="dimmed">Em aberto</Text><Text fz={24} fw={700}>{s.open}</Text></Paper>
          <Paper p="md"><Text size="xs" c="dimmed">Críticos</Text><Text fz={24} fw={700} c={s.critical ? 'red' : undefined}>{s.critical}</Text></Paper>
          <Paper p="md"><Text size="xs" c="dimmed">Novos (não lidos)</Text><Text fz={24} fw={700}>{s.new}</Text></Paper>
          <Paper p="md">
            <Group gap={2}><Text size="xs" c="dimmed">Última verificação</Text>
              <InfoHint label="Verificação automática">O sistema confere as regras de automação a cada hora. Quando a situação se resolve, o alerta é encerrado sozinho na verificação seguinte.</InfoHint>
            </Group>
            <Text fw={600}>{s.lastCheckedAt ? formatDateTime(s.lastCheckedAt) : 'ainda não houve'}</Text>
          </Paper>
        </SimpleGrid>
      )}
      <ListToolbar
        search={list.search}
        onSearch={list.setSearch}
        searchPlaceholder="Buscar por placa ou texto do alerta"
        filters={[
          { key: 'severity', placeholder: 'Gravidade', data: toSelectData(ALERT_SEVERITY), value: f.severity, onChange: (v) => list.setFilter('severity', v) },
          { key: 'category', placeholder: 'Área', data: toSelectData(ALERT_CATEGORY), value: f.category, onChange: (v) => list.setFilter('category', v) },
          { key: 'trigger', placeholder: 'Tipo de alerta', data: toSelectData(TRIGGER_LABEL), value: f.trigger, onChange: (v) => list.setFilter('trigger', v) },
          { key: 'status', placeholder: 'Situação', data: toSelectData(ALERT_STATUS), value: f.status, onChange: (v) => list.setFilter('status', v) },
        ]}
      />
      <Paper p="sm" mb="md" mt={-8}>
        <Group gap="md" wrap="wrap">
          <VehiclePicker aria-label="Veículo" placeholder="Veículo" value={f.vehicleId} onChange={(v) => list.setFilter('vehicleId', v)} style={{ flex: '1 1 220px', maxWidth: 320 }} />
          <Checkbox label="Incluir encerrados" checked={f.includeClosed === 'true'} onChange={(e) => list.setFilter('includeClosed', e.currentTarget.checked ? 'true' : null)} />
          <Checkbox label="Assumidos por mim" checked={f.assignedToMe === 'true'} onChange={(e) => list.setFilter('assignedToMe', e.currentTarget.checked ? 'true' : null)} />
        </Group>
      </Paper>
      <DataTable
        columns={columns}
        data={query.data}
        isLoading={query.isFetching}
        error={query.error}
        onRetry={() => void query.refetch()}
        getRowId={(a) => a.id}
        sort={list.sort}
        onSortChange={list.setSort}
        onPageChange={list.setPage}
        onRowClick={(a) => navigate(`/alertas/${a.id}`)}
        renderCard={(a) => (
          <Stack gap={4}>
            <Group justify="space-between" gap="xs" wrap="nowrap">
              <Text fw={700} lineClamp={2}>{a.title}</Text>
              <SeverityBadge value={a.severity} />
            </Group>
            <Text size="sm" c="dimmed" lineClamp={2}>{a.explanation}</Text>
            <Group gap="xs"><AlertStatusBadge value={a.status} /><Text size="xs" c="dimmed">{formatDateTime(a.detectedAt)}</Text></Group>
          </Stack>
        )}
        emptyState={
          <EmptyState icon={<IconBellRinging size={28} />} title="Nenhum alerta em aberto"
            description="Quando uma regra de automação encontrar algo que precisa de atenção (manutenção atrasada, consumo fora do padrão, orçamento perto do limite…), o alerta aparece aqui." />
        }
        hasFilters={list.hasFilters}
        onClearFilters={list.clearFilters}
      />
    </>
  );
}
