import { useState } from 'react';
import {
  Alert, Badge, Button, Group, Modal, NumberInput, Paper, Select, Skeleton, Stack, Switch, Table, Text, TextInput, Textarea,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconPlayerPlay, IconPlus, IconSettingsAutomation } from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { RowActions, toSelectData } from '../../components/common';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime } from '../../lib/format';
import { usersApi } from '../users/users';
import { automationApi } from './api';
import {
  ALERT_SEVERITY, EVENT_LABEL, NOTIFY_MODE, TRIGGER_LABEL, type AutomationRule, type AutomationRuleRequest, type AutomationTrigger,
  type TriggerDefinition,
} from './alerts';
import { SeverityBadge } from './components';

function conditionText(rule: AutomationRule, definition: TriggerDefinition | undefined) {
  if (rule.trigger === 'OperationalEvent') return rule.eventType ? EVENT_LABEL[rule.eventType] ?? rule.eventType : '—';
  const parts: string[] = [];
  if (definition?.thresholdLabel && rule.threshold !== null) parts.push(`${definition.thresholdLabel} ${rule.threshold.toLocaleString('pt-BR')} ${definition.thresholdUnit ?? ''}`.trim());
  if (rule.lookbackDays) parts.push(`últimos ${rule.lookbackDays} dias`);
  return parts.length ? parts.join(' · ') : 'regra do próprio módulo';
}

export function AutomationRulesPage() {
  const { can } = useAuth();
  const rules = automationApi.useRules();
  const catalog = automationApi.useCatalog();
  const run = automationApi.useRun();
  const remove = automationApi.useRemove();
  const [editing, setEditing] = useState<AutomationRule | 'new' | null>(null);
  const canManage = can(PERMISSIONS.automation.manage);

  const runNow = () => run.mutate(undefined, {
    onSuccess: (r) => notifySuccess(
      `Verificação concluída: ${r.alertsCreated} alerta(s) novo(s), ${r.alertsResolved} encerrado(s) e ${r.notificationsCreated} notificação(ões).`),
    onError: (e) => notifyError(e, 'A verificação não foi concluída'),
  });

  const actions = canManage && (
    <Group gap="sm">
      <Button variant="default" leftSection={<IconPlayerPlay size={16} />} loading={run.isPending} onClick={runNow}>Verificar agora</Button>
      <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing('new')}>Nova regra</Button>
    </Group>
  );

  if (rules.error && !rules.data) return <><PageHeader title="Regras de automação" /><Paper><ErrorState error={rules.error} onRetry={() => void rules.refetch()} /></Paper></>;
  const definitionOf = (t: AutomationTrigger) => catalog.data?.triggers.find((d) => d.trigger === t);

  return (
    <>
      <PageHeader title="Regras de automação"
        description="QUANDO algo acontecer na frota, ENTÃO o sistema cria um alerta e/ou avisa as pessoas. As regras padrão já vêm prontas; ajuste os limites ao jeito da sua empresa."
        action={actions} />
      <Alert variant="light" color="blue" mb="md" icon={<IconSettingsAutomation size={18} />}>
        As regras são verificadas automaticamente a cada hora. Cada alerta só aparece para quem tem permissão de ver aquele tipo de informação,
        e os avisos são apenas dentro do sistema (no sino do cabeçalho). Veja os alertas gerados na <Link to="/alertas">central de alertas</Link>.
      </Alert>
      {!rules.data ? <Stack>{[1, 2, 3].map((i) => <Skeleton key={i} height={48} />)}</Stack> : rules.data.length === 0 ? (
        <Paper><EmptyState icon={<IconSettingsAutomation size={28} />} title="Nenhuma regra" description="Crie uma regra para ser avisado quando algo importante acontecer." action={actions} /></Paper>
      ) : (
        <Paper>
          <Table.ScrollContainer minWidth={820}>
            <Table highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Regra</Table.Th><Table.Th>Condição</Table.Th><Table.Th>Gravidade</Table.Th><Table.Th>Avisar</Table.Th>
                  <Table.Th>Alertas abertos</Table.Th><Table.Th>Última verificação</Table.Th><Table.Th style={{ width: 56 }} />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {rules.data.map((r) => (
                  <Table.Tr key={r.id} style={{ opacity: r.isActive ? 1 : 0.6 }}>
                    <Table.Td>
                      <Text size="sm" fw={600}>{r.name}</Text>
                      <Group gap={4}>
                        {!r.isActive && <Badge color="gray" variant="light">Desativada</Badge>}
                        {r.isSystemDefault && <Badge color="blue" variant="light">Padrão</Badge>}
                      </Group>
                    </Table.Td>
                    <Table.Td><Text size="sm">{conditionText(r, definitionOf(r.trigger))}</Text></Table.Td>
                    <Table.Td><SeverityBadge value={r.severity} /></Table.Td>
                    <Table.Td><Text size="sm">{r.notifyMode === 'SpecificUser' ? r.notifyUserName ?? 'usuário' : NOTIFY_MODE[r.notifyMode].label}</Text></Table.Td>
                    <Table.Td>
                      {r.openAlerts > 0 ? <Link to={`/alertas?ruleId=${r.id}`}>{r.openAlerts}</Link> : <Text size="sm" c="dimmed">0</Text>}
                    </Table.Td>
                    <Table.Td>
                      {r.lastExecution ? (
                        <Stack gap={0}>
                          <Text size="sm">{formatDateTime(r.lastExecution.finishedAt)}</Text>
                          {r.lastExecution.error && <Text size="xs" c="red">{r.lastExecution.error}</Text>}
                        </Stack>
                      ) : <Text size="sm" c="dimmed">ainda não verificada</Text>}
                    </Table.Td>
                    <Table.Td>
                      {canManage && (
                        <RowActions onEdit={() => setEditing(r)} onDelete={r.isSystemDefault ? undefined : () => confirmDelete({
                          entity: 'a regra', name: r.name, onConfirm: () => remove.mutate(r.id, {
                            onSuccess: () => notifySuccess('Regra excluída.'), onError: (e) => notifyError(e, 'A regra não foi excluída'),
                          }),
                        })} />
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Paper>
      )}
      {editing && catalog.data && (
        <RuleModal rule={editing === 'new' ? null : editing} triggers={catalog.data.triggers} events={catalog.data.events} onClose={() => setEditing(null)} />
      )}
    </>
  );
}

function RuleModal({ rule, triggers, events, onClose }: { rule: AutomationRule | null; triggers: TriggerDefinition[]; events: string[]; onClose: () => void }) {
  const mutation = automationApi.useSave(rule?.id);
  const { can } = useAuth();
  const initialValues: AutomationRuleRequest = {
    name: rule?.name ?? '', description: rule?.description ?? null, trigger: rule?.trigger ?? 'OperationalEvent',
    eventType: rule?.eventType ?? null, threshold: rule?.threshold ?? null, lookbackDays: rule?.lookbackDays ?? null,
    severity: rule?.severity ?? 'Warning', createAlert: rule?.createAlert ?? true, notifyMode: rule?.notifyMode ?? 'EveryoneWhoCanSee',
    notifyUserId: rule?.notifyUserId ?? null, isActive: rule?.isActive ?? true,
  };
  const form = useForm<AutomationRuleRequest>({
    initialValues,
    validate: {
      name: (v) => (v.trim() ? null : 'Nome: campo obrigatório.'),
      eventType: (v, values) => (values.trigger === 'OperationalEvent' && !v ? 'Fato: escolha o acontecimento que dispara a regra.' : null),
      notifyUserId: (v, values) => (values.notifyMode === 'SpecificUser' && !v ? 'Usuário: escolha quem será avisado.' : null),
    },
  });
  const definition = triggers.find((d) => d.trigger === form.values.trigger)!;
  const isEvent = definition.kind === 'Event';

  const changeTrigger = (value: string | null) => {
    const d = triggers.find((t) => t.trigger === value);
    if (!d) return;
    form.setValues({
      trigger: d.trigger, threshold: d.defaultThreshold, lookbackDays: d.defaultLookbackDays, severity: d.defaultSeverity,
      createAlert: true, notifyMode: d.defaultNotifyMode, eventType: null, name: form.values.name || d.name,
    });
  };

  return (
    <Modal opened onClose={onClose} title={rule ? 'Editar regra' : 'Nova regra'} centered size="lg">
      <form onSubmit={form.onSubmit((v) => mutation.mutate(
        { ...v, name: v.name.trim(), description: v.description?.trim() || null },
        { onSuccess: () => { notifySuccess('Regra salva com sucesso.'); onClose(); }, onError: (e) => { const err = notifyError(e, 'A regra não foi salva'); form.setErrors(err.fieldErrors); } },
      ))}>
        <Stack gap="md">
          <TextInput label="Nome" withAsterisk maxLength={100} {...form.getInputProps('name')} />
          <Text fw={650} size="sm">Quando</Text>
          <Select label="O que observar" withAsterisk data={triggers.map((t) => ({ value: t.trigger, label: TRIGGER_LABEL[t.trigger].label }))}
            value={form.values.trigger} onChange={changeTrigger} disabled={rule?.isSystemDefault} allowDeselect={false} />
          <Text size="xs" c="dimmed" mt={-8}>{definition.description}</Text>
          {isEvent && (
            <Select label="Fato" withAsterisk data={events.map((e) => ({ value: e, label: EVENT_LABEL[e] ?? e }))}
              disabled={rule?.isSystemDefault} {...form.getInputProps('eventType')} />
          )}
          {definition.thresholdLabel && (
            <NumberInput label={`${definition.thresholdLabel} (${definition.thresholdUnit})`} min={definition.minThreshold ?? undefined}
              max={definition.maxThreshold ?? undefined} decimalSeparator="," thousandSeparator="." allowNegative={false}
              description={`De ${definition.minThreshold} a ${definition.maxThreshold}. Padrão: ${definition.defaultThreshold}.`}
              value={form.values.threshold ?? ''} onChange={(v) => form.setFieldValue('threshold', v === '' ? null : Number(v))} error={form.errors.threshold} />
          )}
          {definition.lookbackLabel && (
            <NumberInput label={`${definition.lookbackLabel} (dias)`} min={7} max={365} allowDecimal={false} allowNegative={false}
              value={form.values.lookbackDays ?? ''} onChange={(v) => form.setFieldValue('lookbackDays', v === '' ? null : Number(v))} error={form.errors.lookbackDays} />
          )}
          <Text fw={650} size="sm">Então</Text>
          <Select label="Gravidade" data={toSelectData(ALERT_SEVERITY)} allowDeselect={false} {...form.getInputProps('severity')} />
          {isEvent && <Switch label="Criar um alerta na central de alertas" {...form.getInputProps('createAlert', { type: 'checkbox' })} />}
          <Select label="Avisar no sino" data={toSelectData(NOTIFY_MODE)} allowDeselect={false} {...form.getInputProps('notifyMode')} />
          {form.values.notifyMode === 'SpecificUser' && (can(PERMISSIONS.users.view)
            ? <NotifyUserSelect {...form.getInputProps('notifyUserId')} />
            : <Text size="sm" c="red">Para escolher um usuário você precisa da permissão de ver usuários.</Text>)}
          <Textarea label="Descrição" autosize minRows={2} maxLength={500} value={form.values.description ?? ''}
            onChange={(e) => form.setFieldValue('description', e.currentTarget.value)} />
          <Switch label="Regra ativa" {...form.getInputProps('isActive', { type: 'checkbox' })}
            description={rule?.isActive ? 'Desativar encerra os alertas abertos desta regra.' : undefined} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>Salvar</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function NotifyUserSelect(props: { value?: string | null; onChange?: (value: string | null) => void; error?: string }) {
  const users = usersApi.useList({ pageSize: 100, status: 'Active' });
  return (
    <Select label="Usuário" withAsterisk searchable data={(users.data?.items ?? []).map((u) => ({ value: u.id, label: u.name }))}
      description="Só recebe o aviso se tiver permissão para ver esse tipo de alerta." {...props} />
  );
}
