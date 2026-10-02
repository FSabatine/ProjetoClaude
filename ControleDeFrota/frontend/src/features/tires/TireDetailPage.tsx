import { useState } from 'react';
import { ActionIcon, Alert, Anchor, Badge, Button, Group, Menu, Pagination, Paper, SimpleGrid, Stack, Table, Text } from '@mantine/core';
import {
  IconAlertTriangle, IconArchive, IconBan, IconCheck, IconChevronDown, IconCircleDot, IconClipboardCheck, IconCoin, IconFileText, IconHistory,
  IconInfoCircle, IconPencil, IconPlus, IconRecycle, IconReplace, IconRoute, IconTool, IconTrash, IconTruckDelivery,
} from '@tabler/icons-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { AttachmentList } from '../../components/Attachments';
import { StatusBadge } from '../../components/common';
import { DetailTabs, HeaderFact, InfoGrid, Section, type DetailTab } from '../../components/DetailLayout';
import { EntityFormPage } from '../../components/EntityFormPage';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { formatCurrency, formatDate, formatDateTime, formatNumber } from '../../lib/format';
import { formatCostPerKm, formatPressure, formatTread } from '../../lib/tires';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { InfoHint } from '../fuel/components';
import { HistoryTimeline } from '../history/HistoryTimeline';
import { tireOperationsApi, tiresApi } from './api';
import { TREAD_HELP } from './AssetTirePanel';
import { DOT_HELP, FIRE_NUMBER_HELP } from './TireFormPage';
import {
  CompleteServiceModal, CorrectionModal, CostModal, DisposeModal, InspectionModal, InstallModal, ReasonModal, RemoveModal, SendToServiceModal,
  StockModal, TransferModal,
} from './TireOperationModals';
import {
  ANOMALY_TYPE, COST_TYPE, DAMAGE_TYPE, DISPOSAL_REASON, INSPECTION_SOURCE, INSTALL_REASON, PRESSURE_CHECK, REMOVAL_DESTINATION, REMOVAL_REASON,
  REPAIR_TYPE, SERVICE_KIND, SERVICE_RESULT, SERVICE_STATUS, TIRE_APPLICATION, TIRE_CONDITION, TIRE_STATUS, WEAR_PATTERN,
  type Tire, type TireAnomaly, type TireInstallation, type TireServiceOrder,
} from './tires';

export const COST_PER_KM_HELP = (
  <>Custo do ciclo de vida (compra + consertos + recapagens + outros custos) dividido pelos quilômetros que o pneu já rodou.
    Só aparece depois de 5.000 km medidos: antes disso o valor da compra domina a conta e o número engana.</>
);

type Operation = 'install' | 'remove' | 'replace' | 'transfer' | 'inspect' | 'repair' | 'retread' | 'repairInPlace' | 'complete' | 'cancelService'
  | 'dispose' | 'evaluation' | 'return' | 'cost' | null;

export function TireDetailPage() {
  const { id } = useParams();
  const detail = tiresApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(t) => t && <TireHub tire={t} />}</EntityFormPage>;
}

/** The tire's hub (seção 29): identity and location in the header, the lifecycle in tabs, loaded on demand (seção 53). */
function TireHub({ tire: t }: { tire: Tire }) {
  const navigate = useNavigate();
  const [op, setOp] = useState<Operation>(null);
  const remove = tiresApi.useRemove();
  const orders = tiresApi.useServiceOrders(t.id);
  const openOrder = orders.data?.find((o) => o.status === 'Open') ?? null;
  const a = t.actions;
  const close = () => setOp(null);
  const cancelService = tireOperationsApi.useCancelService(openOrder?.id ?? '');

  const primary =
    a.canInspect && t.status === 'Installed' ? <Button leftSection={<IconClipboardCheck size={18} />} onClick={() => setOp('inspect')}>Inspecionar</Button>
      : a.canInstall ? <Button leftSection={<IconPlus size={18} />} onClick={() => setOp('install')}>Instalar</Button>
        : a.canManageService && openOrder ? <Button leftSection={<IconCheck size={18} />} onClick={() => setOp('complete')}>Concluir {SERVICE_KIND[openOrder.kind].label.toLowerCase()}</Button>
          : a.canReturnToStock ? <Button leftSection={<IconArchive size={18} />} onClick={() => setOp('return')}>Liberar para uso</Button>
            : null;

  const menu = [
    a.canRemove && <Menu.Item key="r" leftSection={<IconCircleDot size={16} />} onClick={() => setOp('remove')}>Remover do veículo</Menu.Item>,
    a.canReplace && <Menu.Item key="s" leftSection={<IconReplace size={16} />} onClick={() => setOp('replace')}>Substituir por outro pneu</Menu.Item>,
    a.canTransfer && <Menu.Item key="t" leftSection={<IconTruckDelivery size={16} />} onClick={() => setOp('transfer')}>Transferir para outro veículo</Menu.Item>,
    a.canInspect && t.status !== 'Installed' && <Menu.Item key="i" leftSection={<IconClipboardCheck size={16} />} onClick={() => setOp('inspect')}>Inspecionar</Menu.Item>,
    a.canRepairInPlace && <Menu.Item key="rp" leftSection={<IconTool size={16} />} onClick={() => setOp('repairInPlace')}>Registrar conserto no veículo</Menu.Item>,
    a.canSendToRepair && <Menu.Item key="sr" leftSection={<IconTool size={16} />} onClick={() => setOp('repair')}>Enviar para conserto</Menu.Item>,
    a.canSendToRetread && <Menu.Item key="rt" leftSection={<IconRecycle size={16} />} onClick={() => setOp('retread')}>Enviar para recapagem</Menu.Item>,
    a.canManageService && openOrder && <Menu.Item key="cs" leftSection={<IconBan size={16} />} onClick={() => setOp('cancelService')}>Cancelar envio</Menu.Item>,
    a.canSendToEvaluation && <Menu.Item key="ev" leftSection={<IconArchive size={16} />} onClick={() => setOp('evaluation')}>Separar para avaliação</Menu.Item>,
    a.canReturnToStock && <Menu.Item key="rs" leftSection={<IconArchive size={16} />} onClick={() => setOp('return')}>Liberar para uso</Menu.Item>,
    a.canAddCost && <Menu.Item key="c" leftSection={<IconCoin size={16} />} onClick={() => setOp('cost')}>Registrar custo</Menu.Item>,
    a.canDispose && <Menu.Item key="d" color="red" leftSection={<IconBan size={16} />} onClick={() => setOp('dispose')}>Dar baixa</Menu.Item>,
    a.canDelete && <Menu.Item key="x" color="red" leftSection={<IconTrash size={16} />} onClick={() => confirmDelete({
      entity: 'pneu', name: t.code, onConfirm: () => remove.mutate(t.id, {
        onSuccess: () => { notifySuccess('Pneu excluído.'); navigate('/pneus'); }, onError: (e) => notifyError(e, 'O pneu não foi excluído'),
      }),
    })}>Excluir (cadastro por engano)</Menu.Item>,
  ].filter(Boolean);

  const tabs: DetailTab[] = [
    { value: 'visao-geral', label: 'Visão geral', icon: <IconInfoCircle size={16} />, content: <Overview tire={t} /> },
    { value: 'historico', label: 'Histórico', icon: <IconHistory size={16} />, content: <HistoryTimeline owner="tires" id={t.id} /> },
    { value: 'instalacoes', label: 'Instalações', icon: <IconRoute size={16} />, content: <Installations tire={t} /> },
    { value: 'inspecoes', label: 'Inspeções', icon: <IconClipboardCheck size={16} />, content: <Inspections tire={t} /> },
    { value: 'servicos', label: 'Consertos e recapagens', icon: <IconTool size={16} />, content: <ServiceOrders tire={t} orders={orders.data ?? []} /> },
    ...(t.canSeeCosts ? [{ value: 'custos', label: 'Custos', icon: <IconCoin size={16} />, content: <Costs tire={t} onAdd={() => setOp('cost')} /> }] : []),
    { value: 'anexos', label: 'Anexos', icon: <IconFileText size={16} />, content: <Attachments tire={t} /> },
  ];

  const where = t.location
    ? <Anchor component={Link} to={t.location.vehicleId ? `/veiculos/${t.location.vehicleId}?aba=pneus` : `/implementos/${t.location.implementId}`}>{t.location.assetLabel}</Anchor>
    : t.status === 'InStock' || t.status === 'UnderInspection' ? (t.storageLocation ?? 'Estoque') : TIRE_STATUS[t.status].label;

  return (
    <>
      <PageHeader
        title={`Pneu ${t.code}`}
        description={`${t.brand} ${t.modelName} · ${t.size}`}
        breadcrumbs={[{ label: 'Pneus', to: '/pneus' }, { label: t.code }]}
        action={
          <Group gap="xs">
            <AuditHistoryButton entity="Tire" id={t.id} />
            <Button variant="default" leftSection={<IconPencil size={18} />} onClick={() => navigate(`/pneus/${t.id}/editar`)}>
              {a.canEdit ? 'Editar' : 'Ver cadastro'}
            </Button>
            {menu.length > 0 && (
              <Menu position="bottom-end" withinPortal shadow="md">
                <Menu.Target><Button variant="default" rightSection={<IconChevronDown size={16} />}>Ações</Button></Menu.Target>
                <Menu.Dropdown>{menu}</Menu.Dropdown>
              </Menu>
            )}
            {primary}
          </Group>
        }
      />
      <Paper p="md" mb="md">
        <SimpleGrid cols={{ base: 2, md: 5 }} spacing="md">
          <HeaderFact label="Situação" value={<StatusBadge value={t.status} map={TIRE_STATUS} />} />
          <HeaderFact label="Onde está" value={where} hint={t.location ? `${t.location.positionLabel} · desde ${formatDate(t.location.installedAt)}` : t.openServiceOrder?.providerName ?? undefined} />
          <HeaderFact label="Sulco" value={formatTread(t.currentTreadDepthMm)}
            hint={t.treadUsedPercent !== null ? `${t.treadUsedPercent}% gasto` : t.treadMeasuredAt ? `medido em ${formatDate(t.treadMeasuredAt)}` : 'sem medição'} />
          <HeaderFact label="Km rodados" value={t.currentKm !== null ? `${formatNumber(t.currentKm)} km` : '—'}
            hint={t.hasUnmeasuredDistance ? 'inclui período sem km medido' : `${t.retreadCount} recapagem(ns)`} />
          {t.canSeeCosts && t.costs && (
            <HeaderFact label="Custo/km" value={formatCostPerKm(t.costs.costPerKm)} hint={t.costs.costPerKm === null ? t.costs.costPerKmNote : `total ${formatCurrency(t.costs.total)}`} />
          )}
        </SimpleGrid>
      </Paper>
      <DetailTabs tabs={tabs} />

      <InstallModal opened={op === 'install'} onClose={close} tireId={t.id} />
      {t.location && (
        <RemoveModal opened={op === 'remove' || op === 'replace'} onClose={close} tireId={t.id} tireCode={t.code} positionLabel={t.location.positionLabel}
          assetLabel={t.location.assetLabel} showOdometer={!!t.location.vehicleId} replace={op === 'replace'} allowedSize={null} />
      )}
      <TransferModal opened={op === 'transfer'} onClose={close} tireId={t.id} tireCode={t.code} />
      <InspectionModal opened={op === 'inspect'} onClose={close} tireId={t.id} tireCode={t.code} positionLabel={t.location?.positionLabel}
        showOdometer={!!t.location?.vehicleId} />
      <SendToServiceModal opened={op === 'repair'} onClose={close} tireId={t.id} tireCode={t.code} kind="Repair" canSeeCosts={t.canSeeCosts} />
      <SendToServiceModal opened={op === 'retread'} onClose={close} tireId={t.id} tireCode={t.code} kind="Retread" canSeeCosts={t.canSeeCosts} />
      <SendToServiceModal opened={op === 'repairInPlace'} onClose={close} tireId={t.id} tireCode={t.code} kind="Repair" inPlace canSeeCosts={t.canSeeCosts} />
      {openOrder && <CompleteServiceModal opened={op === 'complete'} onClose={close} order={openOrder} canSeeCosts={t.canSeeCosts} />}
      <ReasonModal opened={op === 'cancelService'} onClose={close} title="Cancelar o envio?" saving={cancelService.isPending}
        description="O serviço fica no histórico como cancelado e o pneu volta para avaliação." label="Motivo" confirm="Cancelar envio" color="red"
        onSubmit={(reason, done) => cancelService.mutate(reason, { onSuccess: () => { notifySuccess('Envio cancelado.'); done(); }, onError: (e) => notifyError(e, 'O envio não foi cancelado') })} />
      <DisposeModal opened={op === 'dispose'} onClose={close} tireId={t.id} tireCode={t.code} />
      <StockModal opened={op === 'evaluation'} onClose={close} tireId={t.id} tireCode={t.code} mode="evaluation" />
      <StockModal opened={op === 'return'} onClose={close} tireId={t.id} tireCode={t.code} mode="return" />
      <CostModal opened={op === 'cost'} onClose={close} tireId={t.id} />
    </>
  );
}

function Overview({ tire: t }: { tire: Tire }) {
  const [reviewing, setReviewing] = useState<TireAnomaly | null>(null);
  const review = tireOperationsApi.useReviewAnomaly();
  const open = t.anomalies.filter((x) => !x.reviewedAt);
  return (
    <Stack gap="md">
      {t.status === 'Disposed' && (
        <Alert color="gray" icon={<IconBan size={18} />} title="Pneu baixado">
          {t.disposalReason ? DISPOSAL_REASON[t.disposalReason].label : ''} em {formatDateTime(t.disposedAt)} por {t.disposedByName ?? '—'}
          {t.disposalDestination ? ` · destino: ${t.disposalDestination}` : ''}{t.disposalNotes ? ` · ${t.disposalNotes}` : ''}.
          O histórico completo continua disponível.
        </Alert>
      )}
      {t.openServiceOrder && (
        <Alert color="orange" icon={<IconTool size={18} />} title={`No fornecedor desde ${formatDate(t.openServiceOrder.sentAt)}`}>
          {SERVICE_KIND[t.openServiceOrder.kind].label}{t.openServiceOrder.retreadNumber ? ` nº ${t.openServiceOrder.retreadNumber}` : ''}
          {t.openServiceOrder.providerName ? ` · ${t.openServiceOrder.providerName}` : ''}. Ao receber de volta, conclua o serviço com o resultado.
        </Alert>
      )}
      {t.alerts.map((a) => (
        <Alert key={a.type} color={a.severity === 'Critical' ? 'red' : 'orange'} icon={<IconAlertTriangle size={18} />} p="xs"><Text size="sm">{a.message}</Text></Alert>
      ))}
      {open.length > 0 && (
        <Section title="Requer revisão" description="Sinais calculados pelo sistema. Não indicam a causa: confira e registre o que foi verificado.">
          <Stack gap="xs">
            {open.map((x) => (
              <Group key={x.id} justify="space-between" wrap="wrap" gap="xs">
                <div style={{ minWidth: 0, flex: 1 }}>
                  <Text size="sm" fw={600}>{ANOMALY_TYPE[x.type].label}</Text>
                  <Text size="sm" c="dimmed">{x.message}</Text>
                </div>
                {t.actions.canReviewAnomalies && <Button size="xs" variant="default" onClick={() => setReviewing(x)}>Marcar como revisado</Button>}
              </Group>
            ))}
          </Stack>
        </Section>
      )}
      {t.alerts.length === 0 && open.length === 0 && t.status !== 'Disposed' && <Alert color="teal" icon={<IconInfoCircle />} p="xs">Nenhum alerta para este pneu.</Alert>}

      <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
        <Section title="Identificação" action={<InfoHint label="Número de fogo">{FIRE_NUMBER_HELP}</InfoHint>}>
          <InfoGrid cols={2} items={[
            { label: 'Número de fogo', value: <Text ff="monospace" fw={700} size="sm">{t.code}</Text> },
            { label: 'Série', value: t.serialNumber },
            { label: 'DOT', value: t.dot ? <Group gap={2}>{t.dot}<InfoHint label="DOT">{DOT_HELP}</InfoHint></Group> : null },
            { label: 'Fabricação', value: t.manufacturedOn ? formatDate(t.manufacturedOn) : null },
            { label: 'Aplicação', value: TIRE_APPLICATION[t.application].label },
            { label: 'Carga / velocidade', value: [t.loadIndex, t.speedRating].filter(Boolean).join(' ') || null },
            { label: 'Compra', value: t.purchasedOn ? `${formatDate(t.purchasedOn)}${t.supplier ? ` · ${t.supplier}` : ''}` : t.supplier },
            { label: 'Cadastrado por', value: `${t.createdByName ?? '—'} em ${formatDateTime(t.createdAt)}` },
          ]} />
          {t.notes && <Text size="sm" c="dimmed" style={{ whiteSpace: 'pre-wrap' }}>{t.notes}</Text>}
        </Section>
        <Section title="Vida do pneu" action={<InfoHint label="Sulco">{TREAD_HELP}</InfoHint>}>
          <InfoGrid cols={2} items={[
            { label: 'Sulco atual', value: `${formatTread(t.currentTreadDepthMm)}${t.originalTreadDepthMm ? ` (novo: ${formatTread(t.originalTreadDepthMm)})` : ''}` },
            { label: 'Km rodados', value: t.currentKm !== null ? `${formatNumber(t.currentKm)} km` : '—' },
            { label: 'Recapagens', value: t.retreadCount },
            { label: 'Consertos', value: t.repairCount },
            { label: 'Última inspeção', value: t.lastInspectedAt ? formatDateTime(t.lastInspectedAt) : 'nunca' },
            { label: 'Próxima inspeção', value: t.nextInspectionDue ? formatDate(t.nextInspectionDue) : null },
          ]} />
          {t.hasUnmeasuredDistance && <Text size="xs" c="dimmed">Parte da vida do pneu foi em implemento: esse trecho não tem km medido.</Text>}
        </Section>
      </SimpleGrid>

      {t.canSeeCosts && t.costs && (
        <Section title="Custo do ciclo de vida" action={<InfoHint label="Custo por km">{COST_PER_KM_HELP}</InfoHint>}>
          <InfoGrid cols={4} items={[
            { label: 'Compra', value: formatCurrency(t.costs.purchase) },
            { label: 'Consertos', value: formatCurrency(t.costs.repairs) },
            { label: 'Recapagens', value: formatCurrency(t.costs.retreads) },
            { label: 'Montagem e outros', value: formatCurrency(t.costs.installation + t.costs.other) },
            { label: 'Total', value: <Text fw={700} size="sm">{formatCurrency(t.costs.total)}</Text> },
            { label: 'Km acumulados', value: t.costs.km !== null ? `${formatNumber(t.costs.km)} km` : '—' },
            { label: 'Custo/km', value: formatCostPerKm(t.costs.costPerKm) },
          ]} />
          {t.costs.costPerKmNote && <Text size="xs" c="dimmed">{t.costs.costPerKmNote}</Text>}
        </Section>
      )}

      <ReasonModal opened={!!reviewing} onClose={() => setReviewing(null)} title="Marcar como revisado" saving={review.isPending}
        description={reviewing ? reviewing.message : ''} label="O que foi verificado" placeholder="Ex.: alinhamento conferido, sem problema" confirm="Confirmar revisão"
        onSubmit={(notes, done) => review.mutate({ id: reviewing!.id, notes }, { onSuccess: () => { notifySuccess('Alerta revisado.'); done(); }, onError: (e) => notifyError(e, 'A revisão não foi registrada') })} />
    </Stack>
  );
}

function Installations({ tire }: { tire: Tire }) {
  const [page, setPage] = useState(1);
  const [correcting, setCorrecting] = useState<TireInstallation | null>(null);
  const query = tiresApi.useInstallations(tire.id, { page, pageSize: 20 });
  if (query.data?.totalCount === 0)
    return <Paper><EmptyState icon={<IconRoute size={28} />} title="Nunca instalado" description="As instalações, remoções, rodízios e transferências deste pneu aparecem aqui." /></Paper>;
  return (
    <Paper p="md">
      <Table.ScrollContainer minWidth={760}>
        <Table verticalSpacing="sm">
          <Table.Thead><Table.Tr>
            <Table.Th>Veículo / posição</Table.Th><Table.Th>Instalado</Table.Th><Table.Th>Removido</Table.Th><Table.Th ta="right">Km rodados</Table.Th><Table.Th>Motivo</Table.Th><Table.Th />
          </Table.Tr></Table.Thead>
          <Table.Tbody>
            {(query.data?.items ?? []).map((i) => (
              <Table.Tr key={i.id}>
                <Table.Td>
                  <Anchor component={Link} to={i.vehicleId ? `/veiculos/${i.vehicleId}?aba=pneus` : `/implementos/${i.implementId}`} size="sm" fw={600}>{i.assetLabel}</Anchor>
                  <Text size="xs" c="dimmed">{i.positionLabel}{i.isRotation ? ' · rodízio' : ''}</Text>
                </Table.Td>
                <Table.Td><Text size="sm">{formatDateTime(i.installedAt)}</Text><Text size="xs" c="dimmed">{i.installedOdometerKm !== null ? `${formatNumber(i.installedOdometerKm)} km` : 'sem hodômetro'} · {INSTALL_REASON[i.installReason].label}</Text></Table.Td>
                <Table.Td>{i.removedAt ? <><Text size="sm">{formatDateTime(i.removedAt)}</Text><Text size="xs" c="dimmed">{i.removedOdometerKm !== null ? `${formatNumber(i.removedOdometerKm)} km` : '—'}{i.removalDestination ? ` · ${REMOVAL_DESTINATION[i.removalDestination].label}` : ''}</Text></> : <Badge variant="light">Atual</Badge>}</Table.Td>
                <Table.Td ta="right">{i.isSpare ? <Text size="xs" c="dimmed">estepe</Text> : i.distanceKm !== null ? formatNumber(i.distanceKm) : i.removedAt ? '—' : 'em uso'}</Table.Td>
                <Table.Td><Text size="sm">{i.removalReason ? REMOVAL_REASON[i.removalReason].label : '—'}</Text>{i.removalNotes && <Text size="xs" c="dimmed">{i.removalNotes}</Text>}</Table.Td>
                <Table.Td>{tire.actions.canCorrectHistory && i.vehicleId && !i.isSpare && (
                  <ActionIcon variant="subtle" color="gray" aria-label="Corrigir km" onClick={() => setCorrecting(i)}><IconPencil size={16} /></ActionIcon>
                )}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {(query.data?.totalPages ?? 0) > 1 && <Pagination mt="sm" total={query.data!.totalPages} value={page} onChange={setPage} size="sm" />}
      {correcting && <CorrectionModal opened onClose={() => setCorrecting(null)} installation={correcting} />}
    </Paper>
  );
}

function Inspections({ tire }: { tire: Tire }) {
  const [page, setPage] = useState(1);
  const query = tiresApi.useInspections(tire.id, { page, pageSize: 10 });
  if (query.data?.totalCount === 0)
    return <Paper><EmptyState icon={<IconClipboardCheck size={28} />} title="Nenhuma inspeção" description="As medições de sulco e pressão, danos e fotos ficam guardados aqui — nada é sobrescrito." /></Paper>;
  return (
    <Stack gap="sm">
      {(query.data?.items ?? []).map((i) => (
        <Paper key={i.id} p="md">
          <Group justify="space-between" wrap="wrap" gap="xs" mb={6}>
            <Group gap="xs">
              <Text fw={600} size="sm">{formatDateTime(i.inspectedAt)}</Text>
              <Badge variant="light" color="gray">{INSPECTION_SOURCE[i.source].label}</Badge>
              <StatusBadge value={i.condition} map={TIRE_CONDITION} />
            </Group>
            <Text size="xs" c="dimmed">{i.assetLabel ? `${i.assetLabel} · ${i.positionLabel}` : 'fora do veículo'} · por {i.inspectedByName ?? '—'}</Text>
          </Group>
          <InfoGrid cols={4} items={[
            { label: 'Sulco', value: formatTread(i.treadDepthMm) },
            { label: 'Pressão', value: i.pressure !== null ? <Group gap={4}>{formatPressure(i.pressure, i.pressureUnit)}{i.pressureCheck && <Badge size="xs" variant="light" color={PRESSURE_CHECK[i.pressureCheck].color}>{PRESSURE_CHECK[i.pressureCheck].label}</Badge>}</Group> : '—' },
            { label: 'Desgaste', value: WEAR_PATTERN[i.wearPattern].label },
            { label: 'Km do pneu', value: i.tireKm !== null ? formatNumber(i.tireKm) : '—' },
          ]} />
          {i.damages.length > 0 && <Group gap={4} mt={6}>{i.damages.map((d) => <Badge key={d} color="red" variant="light">{DAMAGE_TYPE[d].label}</Badge>)}</Group>}
          {i.notes && <Text size="sm" mt={6} style={{ whiteSpace: 'pre-wrap' }}>{i.notes}</Text>}
          {i.maintenanceRequestId && <Text size="xs" c="dimmed" mt={4}>Solicitação de manutenção aberta pela regra da empresa.</Text>}
          {i.files.length > 0 && <AttachmentList files={i.files} />}
        </Paper>
      ))}
      {(query.data?.totalPages ?? 0) > 1 && <Pagination total={query.data!.totalPages} value={page} onChange={setPage} size="sm" />}
    </Stack>
  );
}

function ServiceOrders({ tire, orders }: { tire: Tire; orders: TireServiceOrder[] }) {
  if (orders.length === 0)
    return <Paper><EmptyState icon={<IconTool size={28} />} title="Nenhum conserto ou recapagem" description="Envios a fornecedores, resultados, custos e garantias aparecem aqui." /></Paper>;
  return (
    <Stack gap="sm">
      {orders.map((o) => (
        <Paper key={o.id} p="md">
          <Group justify="space-between" wrap="wrap" gap="xs">
            <Group gap="xs">
              <Text fw={600}>{SERVICE_KIND[o.kind].label}{o.retreadNumber ? ` nº ${o.retreadNumber}` : ''}{o.inPlace ? ' (no veículo)' : ''}</Text>
              <StatusBadge value={o.status} map={SERVICE_STATUS} />
              {o.result && <Badge variant="light" color={o.result === 'Approved' ? 'teal' : 'red'}>{SERVICE_RESULT[o.result].label}</Badge>}
            </Group>
            <Text size="xs" c="dimmed">{o.providerName ?? 'fornecedor não informado'}</Text>
          </Group>
          <InfoGrid cols={4} items={[
            { label: 'Enviado', value: formatDateTime(o.sentAt) },
            { label: 'Concluído', value: o.completedAt ? formatDateTime(o.completedAt) : null },
            { label: o.kind === 'Retread' ? 'Banda / sulco novo' : 'Tipo', value: o.kind === 'Retread' ? [o.treadPattern, o.newTreadDepthMm !== null ? formatTread(o.newTreadDepthMm) : null].filter(Boolean).join(' · ') || null : o.repairType ? REPAIR_TYPE[o.repairType].label : null },
            ...(tire.canSeeCosts ? [{ label: 'Valor', value: formatCurrency(o.cost) }] : []),
            { label: 'Garantia até', value: o.warrantyUntil ? formatDate(o.warrantyUntil) : null },
          ]} />
          {(o.description || o.resultNotes || o.cancellationReason) && (
            <Text size="sm" c="dimmed" mt={6}>{[o.description, o.resultNotes, o.cancellationReason && `Cancelado: ${o.cancellationReason}`].filter(Boolean).join(' · ')}</Text>
          )}
          {o.files.length > 0 && <AttachmentList files={o.files} />}
        </Paper>
      ))}
    </Stack>
  );
}

function Costs({ tire, onAdd }: { tire: Tire; onAdd: () => void }) {
  const costs = tiresApi.useCosts(tire.id, tire.canSeeCosts);
  const remove = tiresApi.useDeleteCost(tire.id);
  return (
    <Section title="Custos" description="Compra, consertos, recapagens e outros custos operacionais do pneu."
      action={tire.actions.canAddCost && <Button variant="default" leftSection={<IconPlus size={16} />} onClick={onAdd}>Registrar custo</Button>}>
      <Table.ScrollContainer minWidth={520}>
        <Table verticalSpacing="xs">
          <Table.Thead><Table.Tr><Table.Th>Data</Table.Th><Table.Th>Tipo</Table.Th><Table.Th>Descrição</Table.Th><Table.Th ta="right">Valor</Table.Th><Table.Th /></Table.Tr></Table.Thead>
          <Table.Tbody>
            {tire.purchasePrice !== null && (
              <Table.Tr><Table.Td>{formatDate(tire.purchasedOn)}</Table.Td><Table.Td>Compra</Table.Td><Table.Td>{tire.supplier}</Table.Td><Table.Td ta="right">{formatCurrency(tire.purchasePrice)}</Table.Td><Table.Td /></Table.Tr>
            )}
            {(costs.data ?? []).map((c) => (
              <Table.Tr key={c.id}>
                <Table.Td>{formatDate(c.incurredOn)}</Table.Td>
                <Table.Td>{COST_TYPE[c.type].label}</Table.Td>
                <Table.Td>{c.description}</Table.Td>
                <Table.Td ta="right">{formatCurrency(c.amount)}</Table.Td>
                <Table.Td>{c.canDelete && (
                  <ActionIcon variant="subtle" color="red" aria-label="Excluir custo" onClick={() => confirmDelete({
                    entity: 'custo', name: `${COST_TYPE[c.type].label} de ${formatCurrency(c.amount)}`,
                    onConfirm: () => remove.mutate(c.id, { onSuccess: () => notifySuccess('Custo excluído.'), onError: (e) => notifyError(e, 'O custo não foi excluído') }),
                  })}><IconTrash size={16} /></ActionIcon>
                )}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      {tire.costs && <Text size="sm" fw={600}>Total do ciclo de vida: {formatCurrency(tire.costs.total)} · {formatCostPerKm(tire.costs.costPerKm)}</Text>}
    </Section>
  );
}

function Attachments({ tire }: { tire: Tire }) {
  return (
    <Section title="Anexos do pneu" description="Nota fiscal, garantia e documentos da baixa. As fotos das inspeções ficam em cada inspeção.">
      {tire.files.length > 0 ? <AttachmentList files={tire.files} /> : (
        <Group gap="xs"><IconFileText size={18} /><Text size="sm" c="dimmed">{tire.canSeeCosts || tire.files.length ? 'Nenhum anexo.' : 'Nenhum anexo visível para o seu perfil.'}</Text></Group>
      )}
    </Section>
  );
}
