import { useState } from 'react';
import { Alert, Anchor, Badge, Button, Group, Menu, Paper, Select, SimpleGrid, Skeleton, Stack, Text, Timeline } from '@mantine/core';
import {
  IconAlertTriangle, IconArrowsExchange, IconChevronDown, IconCircleDashed, IconClipboardCheck, IconExternalLink, IconPlus, IconReplace,
  IconSettings, IconTool, IconTruckDelivery,
} from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { HeaderFact, InfoGrid, Section } from '../../components/DetailLayout';
import { notifyError, notifySuccess } from '../../components/notify';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDate, formatDateTime, formatNumber } from '../../lib/format';
import { formatTread } from '../../lib/tires';
import { InfoHint } from '../fuel/components';
import { assetTiresApi, tireLayoutsApi, tireSettingsApi, type AssetRef } from './api';
import { LEVEL_TEXT, levelOf, TireLayoutDiagram } from './TireLayoutDiagram';
import { InspectionModal, InstallModal, RemoveModal, RotationModal, SendToServiceModal, TransferModal } from './TireOperationModals';
import { TIRE_STATUS, type AssetTires, type AssetTirePosition } from './tires';

export const TREAD_HELP = (
  <>
    O <b>sulco</b> é a profundidade dos canais da banda de rodagem, medida em milímetros com um medidor de sulco. Quanto menor, mais
    gasto o pneu. Os limites de “perto do mínimo” e “mínimo” são a <b>política da sua empresa</b> (em Configurações de pneus) — não
    substituem as exigências legais nem as recomendações do fabricante.
  </>
);

type Operation = 'install' | 'remove' | 'replace' | 'transfer' | 'inspect' | 'repair' | 'rotation' | null;

/** The "Pneus" tab of a vehicle and the tire section of an implement (seções 30, 31, 55): one component for both. */
export function AssetTirePanel({ asset }: { asset: AssetRef }) {
  const { can } = useAuth();
  const query = assetTiresApi.useGet(asset);
  const settings = tireSettingsApi.useGet();
  const [selected, setSelected] = useState<string | null>(null);
  const [operation, setOperation] = useState<Operation>(null);

  if (query.isLoading || settings.isLoading) return <Stack>{[1, 2, 3].map((i) => <Skeleton key={i} height={90} />)}</Stack>;
  if (query.error || !query.data) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  const data = query.data;
  const minMm = settings.data?.minTreadDepthMm ?? 3;
  const warningMm = settings.data?.treadWarningDepthMm ?? 4;

  if (!data.layoutId) return <LayoutSetup data={data} asset={asset} />;

  const current = data.positions.find((p) => p.position.code === selected) ?? null;
  const tire = current?.tire ?? null;
  const close = () => setOperation(null);

  return (
    <Stack gap="md">
      <Paper p="md">
        <SimpleGrid cols={{ base: 2, md: 4 }} spacing="md">
          <HeaderFact label="Configuração" value={data.layoutName} hint={<LayoutChanger data={data} asset={asset} />} />
          <HeaderFact label="Pneus instalados" value={`${data.installedCount} de ${data.positions.length}`}
            hint={data.emptyRequiredCount > 0 ? `${data.emptyRequiredCount} posição(ões) obrigatória(s) vazia(s)` : 'todas as obrigatórias preenchidas'} />
          <HeaderFact label="Alertas" value={data.alertCount} hint={data.alertCount > 0 ? 'clique nos pneus marcados' : 'nenhum alerta'} />
          <HeaderFact label="Hodômetro" value={data.currentOdometerKm !== null ? `${formatNumber(data.currentOdometerKm)} km` : 'sem hodômetro'}
            hint={data.vehicleId ? (data.odometerUpdatedAt ? `em ${formatDateTime(data.odometerUpdatedAt)}` : 'sem leitura') : 'implemento: km não medido'} />
        </SimpleGrid>
      </Paper>

      {data.isInactive && <Alert color="gray" p="xs">Cadastro inativo: só é possível remover e inspecionar pneus.</Alert>}

      <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="md">
        <Section title="Posições" description="Toque em uma posição para ver o pneu e as ações."
          action={data.actions.canRotate && (
            <Button variant="default" leftSection={<IconArrowsExchange size={18} />} onClick={() => setOperation('rotation')}>Fazer rodízio</Button>
          )}>
          <TireLayoutDiagram positions={data.positions} minTreadMm={minMm} warningTreadMm={warningMm} selectedCode={selected} onSelect={setSelected} />
        </Section>

        <Section title={current ? current.position.label : 'Selecione uma posição'}
          description={current ? positionHint(current) : 'O detalhe do pneu aparece aqui.'}
          action={current?.tire && <InfoHint label="Sulco">{TREAD_HELP}</InfoHint>}>
          {!current && <Text size="sm" c="dimmed">Escolha uma posição no diagrama.</Text>}
          {current && !tire && (
            <Stack gap="sm" align="flex-start">
              <Group gap="xs"><IconCircleDashed size={18} /><Text size="sm">Nenhum pneu nesta posição.</Text></Group>
              {data.actions.canInstall && <Button leftSection={<IconPlus size={18} />} onClick={() => setOperation('install')}>Instalar pneu</Button>}
            </Stack>
          )}
          {current && tire && (
            <Stack gap="sm">
              <Group justify="space-between" wrap="wrap" gap="xs">
                <Anchor component={Link} to={`/pneus/${tire.tireId}`} fw={700} ff="monospace" size="lg">{tire.code}</Anchor>
                <Badge variant="light" color={levelOf(current, minMm, warningMm) === 'critical' ? 'red' : levelOf(current, minMm, warningMm) === 'warning' ? 'orange' : 'teal'}>
                  {LEVEL_TEXT[levelOf(current, minMm, warningMm)]}
                </Badge>
              </Group>
              <Text size="sm">{tire.brand} {tire.modelName} · {tire.size}</Text>
              <InfoGrid cols={2} items={[
                { label: 'Sulco', value: `${formatTread(tire.currentTreadDepthMm)}${tire.originalTreadDepthMm ? ` de ${formatTread(tire.originalTreadDepthMm)}` : ''}` },
                { label: 'Situação', value: TIRE_STATUS[tire.status].label },
                { label: 'Km do pneu', value: tire.currentKm !== null ? `${formatNumber(tire.currentKm)} km` : '—' },
                { label: 'Km nesta posição', value: tire.kmInPosition !== null ? `${formatNumber(tire.kmInPosition)} km` : 'não medido' },
                { label: 'Instalado em', value: `${formatDate(tire.installedAt)}${tire.installedOdometerKm !== null ? ` · ${formatNumber(tire.installedOdometerKm)} km` : ''}` },
                { label: 'Última inspeção', value: tire.lastInspectedAt ? formatDate(tire.lastInspectedAt) : 'nunca' },
                { label: 'Recapagens', value: tire.retreadCount === 0 ? 'nenhuma (banda original)' : `${tire.retreadCount}` },
              ]} />
              {tire.alerts.map((a) => (
                <Alert key={a.type} color={a.severity === 'Critical' ? 'red' : 'orange'} p="xs" icon={<IconAlertTriangle size={16} />}>
                  <Text size="sm">{a.message}</Text>
                </Alert>
              ))}
              {tire.openAnomalyCount > 0 && (
                <Alert color="orange" p="xs">
                  <Anchor component={Link} to={`/pneus/${tire.tireId}`} size="sm">{tire.openAnomalyCount} sinal(is) requer(em) revisão — ver o pneu.</Anchor>
                </Alert>
              )}
              {tire.compatibilityWarnings.map((w) => <Alert key={w} color="gray" p="xs"><Text size="sm">{w}</Text></Alert>)}
              <Group gap="xs" wrap="wrap">
                {data.actions.canInspect && <Button leftSection={<IconClipboardCheck size={18} />} onClick={() => setOperation('inspect')}>Inspecionar</Button>}
                <Menu position="bottom-start" withinPortal>
                  <Menu.Target><Button variant="default" rightSection={<IconChevronDown size={16} />}>Mais ações</Button></Menu.Target>
                  <Menu.Dropdown>
                    {data.actions.canRemove && <Menu.Item leftSection={<IconCircleDashed size={16} />} onClick={() => setOperation('remove')}>Remover</Menu.Item>}
                    {data.actions.canReplace && <Menu.Item leftSection={<IconReplace size={16} />} onClick={() => setOperation('replace')}>Substituir</Menu.Item>}
                    {data.actions.canReplace && <Menu.Item leftSection={<IconTruckDelivery size={16} />} onClick={() => setOperation('transfer')}>Transferir para outro veículo</Menu.Item>}
                    {can(PERMISSIONS.tires.repair) && (
                      <Menu.Item leftSection={<IconTool size={16} />} onClick={() => setOperation('repair')}>Registrar conserto no veículo</Menu.Item>
                    )}
                    <Menu.Item component={Link} to={`/pneus/${tire.tireId}`} leftSection={<IconExternalLink size={16} />}>Abrir o pneu</Menu.Item>
                  </Menu.Dropdown>
                </Menu>
              </Group>
            </Stack>
          )}
        </Section>
      </SimpleGrid>

      <Section title="Movimentações recentes" description="Instalações, remoções, rodízios e inspeções neste cadastro.">
        {data.recentMovements.length === 0 ? <Text size="sm" c="dimmed">Nenhuma movimentação de pneu ainda.</Text> : (
          <Timeline bulletSize={14} lineWidth={2}>
            {data.recentMovements.map((m) => (
              <Timeline.Item key={m.id} title={<Text size="sm">{m.summary}</Text>}>
                <Text size="xs" c="dimmed">{formatDateTime(m.occurredAt)}</Text>
              </Timeline.Item>
            ))}
          </Timeline>
        )}
      </Section>

      {operation === 'install' && current && <InstallModal opened onClose={close} asset={data} position={current.position} />}
      {tire && current && (
        <>
          <RemoveModal opened={operation === 'remove' || operation === 'replace'} onClose={close} tireId={tire.tireId} tireCode={tire.code}
            positionLabel={current.position.label} assetLabel={data.assetLabel} currentKm={data.currentOdometerKm}
            recommendedPsi={current.position.recommendedPressurePsi} replace={operation === 'replace'} allowedSize={current.position.allowedSize} />
          <TransferModal opened={operation === 'transfer'} onClose={close} tireId={tire.tireId} tireCode={tire.code} />
          <InspectionModal opened={operation === 'inspect'} onClose={close} tireId={tire.tireId} tireCode={tire.code} positionLabel={current.position.label}
            recommendedPsi={current.position.recommendedPressurePsi} currentKm={data.currentOdometerKm} />
          <SendToServiceModal opened={operation === 'repair'} onClose={close} tireId={tire.tireId} tireCode={tire.code} kind="Repair" inPlace canSeeCosts={can(PERMISSIONS.tires.viewcosts)} />
        </>
      )}
      <RotationModal opened={operation === 'rotation'} onClose={close} asset={data} />
    </Stack>
  );
}

function positionHint(p: AssetTirePosition) {
  const parts = [p.position.isRequired ? 'obrigatória' : 'opcional'];
  if (p.position.allowedSize) parts.push(`medida ${p.position.allowedSize}`);
  if (p.position.recommendedPressurePsi) parts.push(`pressão de referência ${p.position.recommendedPressurePsi} psi`);
  return `Posição ${p.position.code} · ${parts.join(' · ')}`;
}

function useLayoutOptions(data: AssetTires) {
  const layouts = tireLayoutsApi.useList(data.vehicleId ? 'Vehicle' : 'Implement');
  return (layouts.data ?? []).map((l) => ({ value: l.id, label: `${l.name} (${l.tirePositionCount} pneus${l.spareCount ? ` + ${l.spareCount} estepe` : ''})` }));
}

/** No configuration yet: explain and let whoever may configure pick one (seção 9). */
function LayoutSetup({ data, asset }: { data: AssetTires; asset: AssetRef }) {
  const options = useLayoutOptions(data);
  const save = assetTiresApi.useSetLayout(asset);
  const [layoutId, setLayoutId] = useState<string | null>(null);
  return (
    <Paper>
      <EmptyState icon={<IconSettings size={28} />} title="Defina a configuração de eixos"
        description={data.actions.canChangeLayout
          ? 'Escolha quantos eixos e pneus este cadastro tem. As posições do diagrama são geradas a partir dela.'
          : 'Este cadastro ainda não tem configuração de eixos. Peça a quem configura os pneus para defini-la.'}
        action={data.actions.canChangeLayout && (
          <Group gap="sm" justify="center" wrap="wrap">
            <Select aria-label="Configuração de eixos" placeholder="Escolha a configuração" data={options} value={layoutId} onChange={setLayoutId} w={320} />
            <Button disabled={!layoutId} loading={save.isPending} onClick={() => save.mutate(layoutId, {
              onSuccess: () => notifySuccess('Configuração de eixos definida.'), onError: (e) => notifyError(e, 'A configuração não foi salva'),
            })}>Usar configuração</Button>
          </Group>
        )} />
    </Paper>
  );
}

function LayoutChanger({ data, asset }: { data: AssetTires; asset: AssetRef }) {
  const options = useLayoutOptions(data);
  const save = assetTiresApi.useSetLayout(asset);
  const [editing, setEditing] = useState(false);
  if (!data.actions.canChangeLayout) return null;
  if (!editing) return <Anchor component="button" size="xs" onClick={() => setEditing(true)}>Alterar</Anchor>;
  return (
    <Select size="xs" aria-label="Nova configuração de eixos" data={options} value={data.layoutId} allowDeselect={false} mt={4}
      onChange={(v) => v && save.mutate(v, {
        onSuccess: () => { notifySuccess('Configuração de eixos alterada.'); setEditing(false); },
        onError: (e) => notifyError(e, 'A configuração não foi alterada'),
      })} onDropdownClose={() => setEditing(false)} />
  );
}
