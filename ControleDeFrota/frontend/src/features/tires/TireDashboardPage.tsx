import type { ReactNode } from 'react';
import { Anchor, Button, Group, Paper, SimpleGrid, Skeleton, Stack, Table, Text, Timeline, UnstyledButton } from '@mantine/core';
import { IconAlertOctagon, IconAlertTriangle, IconCircleDot, IconFileAnalytics, IconPlus } from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { Section } from '../../components/DetailLayout';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { formatCurrency, formatDate, formatDateTime, formatNumber } from '../../lib/format';
import { formatCostPerKm, formatTread } from '../../lib/tires';
import { InfoHint } from '../fuel/components';
import { tireAnalyticsApi } from './api';
import { TREAD_HELP } from './AssetTirePanel';
import { COST_PER_KM_HELP } from './TireDetailPage';

/** A KPI that is also a shortcut to the list already filtered (UX_UI.md: every number leads to its records). */
function Kpi({ label, value, to, hint, tone, help }: { label: string; value: number; to: string; hint?: string; tone?: 'warning' | 'critical'; help?: ReactNode }) {
  const navigate = useNavigate();
  const Icon = tone === 'critical' ? IconAlertOctagon : tone === 'warning' ? IconAlertTriangle : null;
  return (
    <Paper p="md">
      <UnstyledButton onClick={() => navigate(to)} aria-label={`${label}: ${value}. Ver a lista`} style={{ display: 'block', width: '100%' }}>
        <Group gap={4} wrap="nowrap">
          {Icon && value > 0 && <Icon size={14} color={`var(--mantine-color-${tone === 'critical' ? 'red' : 'orange'}-filled)`} aria-hidden />}
          <Text size="xs" c="dimmed" fw={500} tt="uppercase">{label}</Text>
          {help && <span onClick={(e) => e.stopPropagation()}><InfoHint label={label}>{help}</InfoHint></span>}
        </Group>
        <Text fw={650} fz={{ base: 22, sm: 26 }}>{formatNumber(value)}</Text>
        {hint && <Text size="xs" c="dimmed">{hint}</Text>}
      </UnstyledButton>
    </Paper>
  );
}

/** Tire dashboard (seção 44): what exists, what needs action now, what costs most and what happened recently. */
export function TireDashboardPage() {
  const { can } = useAuth();
  const navigate = useNavigate();
  const query = tireAnalyticsApi.useDashboard();
  const d = query.data;

  const actions = (
    <Group gap="xs">
      <Button variant="default" leftSection={<IconFileAnalytics size={18} />} onClick={() => navigate('/pneus/relatorios')}>Relatórios</Button>
      {can(PERMISSIONS.tires.create) && <Button leftSection={<IconPlus size={18} />} onClick={() => navigate('/pneus/novo')}>Cadastrar pneu</Button>}
    </Group>
  );

  if (query.error && !d) return <><PageHeader title="Painel de pneus" action={actions} /><ErrorState error={query.error} onRetry={() => void query.refetch()} /></>;
  if (!d) return <><PageHeader title="Painel de pneus" action={actions} /><SimpleGrid cols={{ base: 2, md: 4 }}>{[1, 2, 3, 4, 5, 6, 7, 8].map((i) => <Skeleton key={i} height={92} />)}</SimpleGrid></>;

  if (d.status.total === 0 && d.status.disposed === 0)
    return (
      <>
        <PageHeader title="Painel de pneus" description="Situação, alertas e custos dos pneus da frota." action={actions} />
        <Paper><EmptyState icon={<IconCircleDot size={28} />} title="Nenhum pneu cadastrado ainda"
          description="Cadastre os pneus pelo número de fogo e instale-os nos veículos pela aba Pneus para acompanhar sulco, km e custos." action={actions} /></Paper>
      </>
    );

  return (
    <>
      <PageHeader title="Painel de pneus" description="Situação, alertas e custos dos pneus da frota." action={actions} />
      <Stack gap="md">
        <Text fw={650}>Pneus da frota</Text>
        <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }} spacing="md">
          <Kpi label="Total em uso e estoque" value={d.status.total} to="/pneus" hint={`${d.status.disposed} baixado(s) no histórico`} />
          <Kpi label="Instalados" value={d.status.installed} to="/pneus?status=Installed" />
          <Kpi label="Em estoque" value={d.status.inStock} to="/pneus?status=InStock" />
          <Kpi label="Em avaliação" value={d.status.underInspection} to="/pneus?status=UnderInspection" />
          <Kpi label="Em conserto" value={d.status.underRepair} to="/pneus?status=UnderRepair" />
          <Kpi label="Em recapagem" value={d.status.underRetread} to="/pneus?status=UnderRetread" />
        </SimpleGrid>

        <Text fw={650} mt="sm">O que requer atenção</Text>
        <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }} spacing="md">
          <Kpi label="No sulco mínimo" value={d.attention.belowMinimum} to="/pneus?alert=BelowMinimum" tone="critical"
            hint={`mínimo da empresa: ${formatTread(d.minTreadDepthMm)}`} help={TREAD_HELP} />
          <Kpi label="Perto da substituição" value={d.attention.nearReplacement} to="/pneus?alert=NearReplacement" tone="warning"
            hint={`sulco até ${formatTread(d.treadWarningDepthMm)}`} />
          <Kpi label="Inspeção atrasada" value={d.attention.inspectionOverdue} to="/pneus?inspection=Overdue" tone="warning"
            hint={d.inspectionIntervalDays > 0 ? `a cada ${d.inspectionIntervalDays} dias` : 'intervalo desligado'} />
          <Kpi label="Dano ou desgaste irregular" value={d.attention.damageOrUnevenWear} to="/pneus?alert=DamageOrUnevenWear" tone="critical" />
          <Kpi label="Requer revisão" value={d.attention.requiresReview} to="/pneus?alert=RequiresReview" tone="warning" hint="sinais calculados" />
          <Kpi label="Idade acima do limite" value={d.attention.ageExceeded} to="/pneus?alert=AgeExceeded" tone="warning" />
        </SimpleGrid>

        <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="md">
          <Section title="Desgaste anormal ou dano" description="Pneus em uso ou estoque com registro na última inspeção ou perda de sulco acima da referência.">
            {d.abnormalWear.length === 0 ? <Text size="sm" c="dimmed">Nenhum pneu nesta situação.</Text> : (
              <Table.ScrollContainer minWidth={420}>
                <Table verticalSpacing="xs">
                  <Table.Tbody>
                    {d.abnormalWear.map((t) => (
                      <Table.Tr key={t.tireId}>
                        <Table.Td><Anchor component={Link} to={`/pneus/${t.tireId}`} ff="monospace" fw={700} size="sm">{t.code}</Anchor><Text size="xs" c="dimmed">{t.brand} {t.modelName}</Text></Table.Td>
                        <Table.Td><Text size="sm">{t.assetLabel ? `${t.assetLabel} · ${t.positionLabel}` : 'fora do veículo'}</Text></Table.Td>
                        <Table.Td><Text size="sm">{t.detail}</Text><Text size="xs" c="dimmed">sulco {formatTread(t.currentTreadDepthMm)}</Text></Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            )}
          </Section>
          <Section title="Próximas inspeções" description="Pneus instalados com inspeção vencendo nos próximos 7 dias ou atrasada.">
            {d.upcomingInspections.length === 0 ? <Text size="sm" c="dimmed">Nada vencendo nos próximos dias.</Text> : (
              <Stack gap={6}>
                {d.upcomingInspections.map((i) => (
                  <Group key={i.tireId} justify="space-between" wrap="nowrap" gap="xs">
                    <div style={{ minWidth: 0 }}>
                      <Anchor component={Link} to={`/pneus/${i.tireId}`} ff="monospace" fw={700} size="sm">{i.code}</Anchor>
                      <Text size="xs" c="dimmed" truncate>{i.assetLabel} · {i.positionLabel}</Text>
                    </div>
                    <Text size="sm" c={i.overdue ? 'red' : undefined} fw={i.overdue ? 600 : undefined} style={{ flexShrink: 0 }}>
                      {i.overdue ? `atrasada desde ${formatDate(i.dueAt)}` : `até ${formatDate(i.dueAt)}`}
                    </Text>
                  </Group>
                ))}
              </Stack>
            )}
          </Section>
        </SimpleGrid>

        <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="md">
          {d.canSeeCosts && d.highestCostPerKm && (
            <Section title="Maior custo por km" description="Entre pneus em uso com pelo menos 5.000 km medidos." action={<InfoHint label="Custo por km">{COST_PER_KM_HELP}</InfoHint>}>
              {d.highestCostPerKm.length === 0 ? <Text size="sm" c="dimmed">Ainda não há pneus com km suficiente para calcular.</Text> : (
                <Table.ScrollContainer minWidth={420}>
                  <Table verticalSpacing="xs">
                    <Table.Thead><Table.Tr><Table.Th>Pneu</Table.Th><Table.Th ta="right">Custo total</Table.Th><Table.Th ta="right">Km</Table.Th><Table.Th ta="right">Custo/km</Table.Th></Table.Tr></Table.Thead>
                    <Table.Tbody>
                      {d.highestCostPerKm.map((t) => (
                        <Table.Tr key={t.tireId}>
                          <Table.Td><Anchor component={Link} to={`/pneus/${t.tireId}`} ff="monospace" fw={700} size="sm">{t.code}</Anchor><Text size="xs" c="dimmed">{t.brand} {t.modelName}</Text></Table.Td>
                          <Table.Td ta="right">{formatCurrency(t.totalCost)}</Table.Td>
                          <Table.Td ta="right">{formatNumber(t.km)}</Table.Td>
                          <Table.Td ta="right">{formatCostPerKm(t.costPerKm)}</Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Table.ScrollContainer>
              )}
            </Section>
          )}
          <Section title="Movimentações recentes" description="Instalações, remoções, rodízios e inspeções.">
            {d.recentMovements.length === 0 ? <Text size="sm" c="dimmed">Nenhuma movimentação ainda.</Text> : (
              <Timeline bulletSize={14} lineWidth={2}>
                {d.recentMovements.map((m) => (
                  <Timeline.Item key={m.id} title={m.tireId ? <Anchor component={Link} to={`/pneus/${m.tireId}`} size="sm" c="inherit">{m.summary}</Anchor> : <Text size="sm">{m.summary}</Text>}>
                    <Text size="xs" c="dimmed">{formatDateTime(m.occurredAt)}</Text>
                  </Timeline.Item>
                ))}
              </Timeline>
            )}
          </Section>
        </SimpleGrid>
      </Stack>
    </>
  );
}
