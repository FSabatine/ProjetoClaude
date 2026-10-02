import { useEffect, useState, type ReactNode } from 'react';
import {
  Alert, Button, Chip, Group, Modal, NumberInput, Radio, SegmentedControl, Select, SimpleGrid, Stack, Table, Text, TextInput, Textarea,
} from '@mantine/core';
import { DateInput, DateTimePicker } from '@mantine/dates';
import { useMediaQuery } from '@mantine/hooks';
import { IconAlertTriangle, IconInfoCircle } from '@tabler/icons-react';
import dayjs from 'dayjs';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { toSelectData } from '../../components/common';
import { ImplementPicker, TirePicker, VehiclePicker, WorkshopPicker } from '../../components/EntityPickers';
import { notifyError, notifySuccess } from '../../components/notify';
import { formatNumber } from '../../lib/format';
import { formatTread, fromPsi, pressureCheck, rotationProblems, TREAD_LEVEL_TEXT, treadLevel, type PressureUnit } from '../../lib/tires';
import type { FileInfo } from '../operations/api';
import { assetTiresApi, tireOperationsApi, tiresApi, tireSettingsApi, type AssetRef } from './api';
import {
  COST_TYPE, DAMAGE_TYPE, DISPOSAL_REASON, PRESSURE_CHECK, PRESSURE_UNIT, REMOVAL_DESTINATION, REMOVAL_REASON, REPAIR_TYPE,
  TIRE_CONDITION, WEAR_PATTERN, type AssetTires, type DamageType, type RemovalDestination, type ServiceKind, type TireCondition,
  type TireInstallation, type TirePosition, type TireServiceOrder, type WearPattern,
} from './tires';

type NumberValue = number | string;
const num = (v: NumberValue) => (v === '' || v === null || v === undefined ? null : Number(v));
const iso = (d: Date | null) => (d ? dayjs(d).toISOString() : null);

/** Field operations are used on phones: full-screen modal there, centered dialog on larger screens (seção 58). */
function OperationModal({ opened, onClose, title, children }: { opened: boolean; onClose: () => void; title: string; children: ReactNode }) {
  const isPhone = useMediaQuery('(max-width: 48em)');
  return (
    <Modal opened={opened} onClose={onClose} title={title} size="lg" fullScreen={isPhone} centered>
      {children}
    </Modal>
  );
}

function Actions({ onClose, saving, label, color, disabled }: { onClose: () => void; saving: boolean; label: string; color?: string; disabled?: boolean }) {
  return (
    <Group justify="flex-end" mt="md">
      <Button variant="default" onClick={onClose} disabled={saving}>Voltar</Button>
      <Button type="submit" loading={saving} color={color} disabled={disabled}>{label}</Button>
    </Group>
  );
}

/** "Quando" + "Hodômetro" pair shared by the operations: blank = now / the odometer history (seção 38). */
function WhenFields({ when, setWhen, km, setKm, currentKm, kmError, showKm = true }: {
  when: Date | null; setWhen: (d: Date | null) => void; km: NumberValue; setKm: (v: NumberValue) => void; currentKm?: number | null;
  kmError?: string | null; showKm?: boolean;
}) {
  return (
    <SimpleGrid cols={{ base: 1, sm: showKm ? 2 : 1 }}>
      <DateTimePicker label="Quando" placeholder="Agora" valueFormat="DD/MM/YYYY HH:mm" clearable maxDate={new Date()} value={when} onChange={setWhen}
        description="Deixe em branco se foi agora." />
      {showKm && (
        <NumberInput label="Hodômetro do veículo (opcional)" suffix=" km" thousandSeparator="." decimalSeparator="," allowNegative={false}
          allowDecimal={false} inputMode="numeric" value={km} onChange={setKm} error={kmError}
          description={currentKm !== undefined && currentKm !== null ? `Atual: ${formatNumber(currentKm)} km. Em branco = usa o histórico.` : 'Em branco = usa o histórico de hodômetro.'} />
      )}
    </SimpleGrid>
  );
}

function CompatibilityNote({ tireId, target, positionCode }: { tireId: string | null; target: { vehicleId?: string | null; implementId?: string | null }; positionCode: string | null }) {
  const compatibility = tiresApi.useCompatibility(tireId, target, positionCode);
  const data = compatibility.data;
  if (!data) return null;
  if (data.status === 'Compatible') return <Alert color="teal" p="xs" icon={<IconInfoCircle size={16} />}>Medida conferida com a configuração da posição.</Alert>;
  const color = data.status === 'Incompatible' ? 'red' : data.status === 'Warning' ? 'orange' : 'gray';
  return (
    <Alert color={color} p="xs" icon={<IconAlertTriangle size={16} />}
      title={data.status === 'Incompatible' ? 'Não pode ser instalado nesta posição' : data.status === 'Warning' ? 'Confira antes de instalar' : 'Compatibilidade não verificada'}>
      <Stack gap={2}>{data.messages.map((m) => <Text key={m} size="sm">{m}</Text>)}</Stack>
    </Alert>
  );
}

// ---------- install ----------

/** From a position (pick a tire in stock) or from a tire (pick vehicle/implement and position). */
export function InstallModal({ opened, onClose, asset, position, tireId: fixedTireId }: {
  opened: boolean; onClose: () => void; asset?: AssetTires; position?: TirePosition; tireId?: string;
}) {
  const [tireId, setTireId] = useState<string | null>(fixedTireId ?? null);
  const [kind, setKind] = useState<'vehicles' | 'implements'>('vehicles');
  const [assetId, setAssetId] = useState<string | null>(null);
  const [positionCode, setPositionCode] = useState<string | null>(position?.code ?? null);
  const [when, setWhen] = useState<Date | null>(null);
  const [km, setKm] = useState<NumberValue>('');
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const install = tiresApi.useInstall(tireId ?? '');
  const chosen: AssetRef | null = asset ? { kind: asset.vehicleId ? 'vehicles' : 'implements', id: (asset.vehicleId ?? asset.implementId)! }
    : assetId ? { kind, id: assetId } : null;
  const target = { vehicleId: chosen?.kind === 'vehicles' ? chosen.id : null, implementId: chosen?.kind === 'implements' ? chosen.id : null };

  useEffect(() => {
    if (opened) { setTireId(fixedTireId ?? null); setPositionCode(position?.code ?? null); setErrors({}); setWhen(null); setKm(''); setNotes(''); }
  }, [opened, fixedTireId, position?.code]);

  const submit = () => {
    if (!tireId) return setErrors({ tireId: 'Selecione o pneu.' });
    if (!chosen || !positionCode) return setErrors({ positionCode: 'Selecione o veículo/implemento e a posição.' });
    install.mutate({ ...target, positionCode, performedAt: iso(when), odometerKm: num(km), notes: notes || null }, {
      onSuccess: () => { notifySuccess('Pneu instalado.'); onClose(); },
      onError: (e) => setErrors(notifyError(e, 'O pneu não foi instalado').fieldErrors),
    });
  };

  return (
    <OperationModal opened={opened} onClose={onClose} title={position ? `Instalar pneu · ${position.label}` : 'Instalar pneu'}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          {!fixedTireId && (
            <TirePicker label="Pneu em estoque" withAsterisk value={tireId} onChange={setTireId} tireSize={position?.allowedSize} error={errors.tireId}
              description={position?.allowedSize ? `Só pneus da medida ${position.allowedSize}, exigida nesta posição.` : 'Pneus disponíveis em estoque.'} />
          )}
          {!asset && <AssetAndPosition kind={kind} setKind={setKind} assetId={assetId} setAssetId={setAssetId} positionCode={positionCode}
            setPositionCode={setPositionCode} error={errors.positionCode ?? errors.vehicleId} onlyFree />}
          {asset && errors.positionCode && <Text size="sm" c="red">{errors.positionCode}</Text>}
          <CompatibilityNote tireId={tireId} target={target} positionCode={positionCode} />
          <WhenFields when={when} setWhen={setWhen} km={km} setKm={setKm} currentKm={asset?.currentOdometerKm} kmError={errors.odometerKm ?? errors.performedAt}
            showKm={!asset || !!asset.vehicleId} />
          <Textarea label="Observações" autosize minRows={2} maxLength={1000} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />
          <Actions onClose={onClose} saving={install.isPending} label="Instalar pneu" />
        </Stack>
      </form>
    </OperationModal>
  );
}

/** Vehicle or implement + one of its positions (free ones only when installing). */
function AssetAndPosition({ kind, setKind, assetId, setAssetId, positionCode, setPositionCode, error, onlyFree }: {
  kind: 'vehicles' | 'implements'; setKind: (k: 'vehicles' | 'implements') => void; assetId: string | null; setAssetId: (v: string | null) => void;
  positionCode: string | null; setPositionCode: (v: string | null) => void; error?: string; onlyFree?: boolean;
}) {
  return (
    <Stack gap="xs">
      <SegmentedControl value={kind} onChange={(v) => { setKind(v as 'vehicles' | 'implements'); setAssetId(null); setPositionCode(null); }}
        data={[{ value: 'vehicles', label: 'Veículo' }, { value: 'implements', label: 'Implemento' }]} />
      {kind === 'vehicles'
        ? <VehiclePicker label="Veículo" withAsterisk value={assetId} onChange={(v) => { setAssetId(v); setPositionCode(null); }} />
        : <ImplementPicker label="Implemento" withAsterisk value={assetId} onChange={(v) => { setAssetId(v); setPositionCode(null); }} />}
      {assetId && <PositionSelect asset={{ kind, id: assetId }} value={positionCode} onChange={setPositionCode} error={error} onlyFree={onlyFree} />}
      {!assetId && error && <Text size="sm" c="red">{error}</Text>}
    </Stack>
  );
}

function PositionSelect({ asset, value, onChange, error, onlyFree }: { asset: AssetRef; value: string | null; onChange: (v: string | null) => void; error?: string; onlyFree?: boolean }) {
  const tires = assetTiresApi.useGet(asset);
  if (tires.data && !tires.data.layoutId)
    return <Alert color="orange" p="xs">Este cadastro ainda não tem configuração de eixos. Defina-a na aba Pneus antes de instalar.</Alert>;
  const data = (tires.data?.positions ?? []).map((p) => ({
    value: p.position.code,
    label: `${p.position.label}${p.tire ? ` — ocupada (${p.tire.code})` : ''}`,
    disabled: onlyFree && !!p.tire,
  }));
  return <Select label="Posição" withAsterisk data={data} value={value} onChange={onChange} error={error} placeholder={tires.isLoading ? 'Carregando…' : 'Escolha a posição'} />;
}

// ---------- removal and replacement ----------

/** Measurement block shared by removal and inspection (tread, pressure, condition, wear, damages). */
function MeasurementFields({ m, set, recommendedPsi, minMm, warningMm, tolerance, requireCondition }: {
  m: Measurement; set: (m: Measurement) => void; recommendedPsi?: number | null; minMm: number; warningMm: number; tolerance: number; requireCondition?: boolean;
}) {
  const level = treadLevel(num(m.tread), minMm, warningMm);
  const check = pressureCheck(num(m.pressure), m.unit, recommendedPsi ?? null, tolerance);
  return (
    <Stack gap="sm">
      <SimpleGrid cols={{ base: 1, sm: 2 }}>
        <NumberInput label="Sulco (mm)" size="md" decimalScale={1} decimalSeparator="," allowNegative={false} inputMode="decimal" max={40}
          value={m.tread} onChange={(v) => set({ ...m, tread: v })}
          description={num(m.tread) !== null ? `${TREAD_LEVEL_TEXT[level]} (mínimo da empresa: ${formatTread(minMm)})` : 'Menor medida entre os sulcos.'} />
        <Group gap="xs" align="flex-end" wrap="nowrap">
          <NumberInput label="Pressão" size="md" decimalScale={2} decimalSeparator="," allowNegative={false} inputMode="decimal" style={{ flex: 1 }}
            value={m.pressure} onChange={(v) => set({ ...m, pressure: v })}
            description={check ? PRESSURE_CHECK[check].label + (recommendedPsi ? ` (${fromPsi(recommendedPsi, m.unit)} ${PRESSURE_UNIT[m.unit].label})` : '') : 'Medida com calibrador.'} />
          <SegmentedControl size="sm" value={m.unit} onChange={(v) => set({ ...m, unit: v as PressureUnit })} data={toSelectData(PRESSURE_UNIT)} aria-label="Unidade da pressão" />
        </Group>
      </SimpleGrid>
      <div>
        <Text size="sm" fw={500} mb={4}>Condição visual{requireCondition && <Text span c="red"> *</Text>}</Text>
        <SegmentedControl fullWidth size="md" value={m.condition ?? ''} onChange={(v) => set({ ...m, condition: v as TireCondition })}
          data={toSelectData(TIRE_CONDITION)} aria-label="Condição visual" />
      </div>
      <Select label="Desgaste observado" data={toSelectData(WEAR_PATTERN)} value={m.wear} onChange={(v) => set({ ...m, wear: (v ?? 'Normal') as WearPattern })}
        description="Registre o que viu — a causa é avaliada depois." />
      <div>
        <Text size="sm" fw={500} mb={4}>Danos encontrados</Text>
        <Chip.Group multiple value={m.damages} onChange={(v) => set({ ...m, damages: v as DamageType[] })}>
          <Group gap="xs">{(Object.keys(DAMAGE_TYPE) as DamageType[]).map((d) => <Chip key={d} value={d} size="md">{DAMAGE_TYPE[d].label}</Chip>)}</Group>
        </Chip.Group>
      </div>
    </Stack>
  );
}

interface Measurement {
  tread: NumberValue;
  pressure: NumberValue;
  unit: PressureUnit;
  condition: TireCondition | null;
  wear: WearPattern;
  damages: DamageType[];
}

const emptyMeasurement = (unit: PressureUnit = 'Psi'): Measurement => ({ tread: '', pressure: '', unit, condition: null, wear: 'Unknown', damages: [] });

export function RemoveModal({ opened, onClose, tireId, tireCode, positionLabel, assetLabel, currentKm, showOdometer, recommendedPsi, replace, allowedSize }: {
  opened: boolean; onClose: () => void; tireId: string; tireCode: string; positionLabel: string; assetLabel: string; currentKm?: number | null;
  /** Vehicles have an odometer, implements do not. Defaults to "a current km was given". */
  showOdometer?: boolean; recommendedPsi?: number | null; replace?: boolean; allowedSize?: string | null;
}) {
  const settings = tireSettingsApi.useGet();
  const remove = tiresApi.useRemoveFromAsset(tireId);
  const swap = tiresApi.useReplace(tireId);
  const [reason, setReason] = useState<string | null>(replace ? 'Replacement' : null);
  const [destination, setDestination] = useState<RemovalDestination>('Stock');
  const [m, setM] = useState<Measurement>(emptyMeasurement());
  const [when, setWhen] = useState<Date | null>(null);
  const [km, setKm] = useState<NumberValue>('');
  const [storage, setStorage] = useState('');
  const [workshopId, setWorkshopId] = useState<string | null>(null);
  const [providerName, setProviderName] = useState('');
  const [repairType, setRepairType] = useState<string | null>(null);
  const [disposalReason, setDisposalReason] = useState<string | null>(null);
  const [disposalDestination, setDisposalDestination] = useState('');
  const [replacementId, setReplacementId] = useState<string | null>(null);
  const [notes, setNotes] = useState('');
  const [files, setFiles] = useState<FileInfo[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const s = settings.data;

  useEffect(() => {
    if (!opened) return;
    setReason(replace ? 'Replacement' : null); setDestination('Stock'); setM(emptyMeasurement(s?.pressureUnit)); setWhen(null); setKm(''); setStorage('');
    setWorkshopId(null); setProviderName(''); setRepairType(null); setDisposalReason(null); setDisposalDestination(''); setReplacementId(null);
    setNotes(''); setFiles([]); setErrors({});
  }, [opened, replace, s?.pressureUnit]);

  const submit = () => {
    const next: Record<string, string> = {};
    if (!reason) next.reason = 'Informe o motivo da remoção.';
    if (destination === 'Disposal' && !disposalReason) next.disposalReason = 'Informe o motivo da baixa.';
    if (replace && !replacementId) next.replacementTireId = 'Selecione o pneu que será instalado.';
    if (Object.keys(next).length) return setErrors(next);
    const body = {
      performedAt: iso(when), odometerKm: num(km), reason: reason!, destination, treadDepthMm: num(m.tread), pressure: num(m.pressure),
      pressureUnit: num(m.pressure) !== null ? m.unit : null, condition: m.condition, wearPattern: m.wear === 'Unknown' ? null : m.wear, damages: m.damages,
      notes: notes || null, storageLocation: storage || null, workshopId, providerName: providerName || null, repairType,
      disposalReason, disposalDestination: disposalDestination || null, fileIds: files.map((f) => f.id),
    };
    const handlers = {
      onSuccess: () => { notifySuccess(replace ? 'Pneu substituído. A remoção do anterior ficou registrada.' : 'Pneu removido.'); onClose(); },
      onError: (e: unknown) => setErrors(notifyError(e, replace ? 'A substituição não foi feita' : 'O pneu não foi removido').fieldErrors),
    };
    if (replace) swap.mutate({ ...body, replacementTireId: replacementId! }, handlers);
    else remove.mutate(body, handlers);
  };

  const destinations = (Object.keys(REMOVAL_DESTINATION) as RemovalDestination[]).filter((d) => d !== 'Installed');
  return (
    <OperationModal opened={opened} onClose={onClose} title={`${replace ? 'Substituir' : 'Remover'} pneu ${tireCode} · ${positionLabel}`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <Text size="sm" c="dimmed">{assetLabel} · {positionLabel}. A remoção fica no histórico do pneu com o km rodado nesta posição.</Text>
          {replace && (
            <>
              <TirePicker label="Pneu que entra na posição" withAsterisk value={replacementId} onChange={setReplacementId} tireSize={allowedSize} error={errors.replacementTireId}
                description={allowedSize ? `Só pneus da medida ${allowedSize}.` : 'Pneus em estoque.'} />
            </>
          )}
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <Select label="Motivo" withAsterisk data={toSelectData(REMOVAL_REASON).filter((o) => o.value !== 'Rotation' && o.value !== 'Transfer')}
              value={reason} onChange={setReason} error={errors.reason} />
            <Select label="Destino do pneu retirado" withAsterisk allowDeselect={false}
              data={destinations.map((d) => ({ value: d, label: REMOVAL_DESTINATION[d].label }))} value={destination}
              onChange={(v) => setDestination((v ?? 'Stock') as RemovalDestination)} error={errors.destination} />
          </SimpleGrid>
          {(destination === 'Stock' || destination === 'Evaluation') && (
            <TextInput label="Local de armazenamento" placeholder="Ex.: Almoxarifado A · Prateleira 3" maxLength={120} value={storage} onChange={(e) => setStorage(e.currentTarget.value)} />
          )}
          {(destination === 'Repair' || destination === 'Retread') && (
            <SimpleGrid cols={{ base: 1, sm: 2 }}>
              <WorkshopPicker label="Fornecedor (cadastrado)" value={workshopId} onChange={setWorkshopId} error={errors.workshopId} />
              <TextInput label="ou nome do fornecedor" maxLength={120} value={providerName} onChange={(e) => setProviderName(e.currentTarget.value)} />
              {destination === 'Repair' && <Select label="Tipo de conserto" data={toSelectData(REPAIR_TYPE)} value={repairType} onChange={setRepairType} clearable />}
            </SimpleGrid>
          )}
          {destination === 'Disposal' && (
            <SimpleGrid cols={{ base: 1, sm: 2 }}>
              <Select label="Motivo da baixa" withAsterisk data={toSelectData(DISPOSAL_REASON)} value={disposalReason} onChange={setDisposalReason} error={errors.disposalReason} />
              <TextInput label="Destino (opcional)" placeholder="Ex.: reciclagem, venda" maxLength={500} value={disposalDestination} onChange={(e) => setDisposalDestination(e.currentTarget.value)} />
            </SimpleGrid>
          )}
          <Text size="sm" fw={600}>Medição na remoção (opcional)</Text>
          {s && <MeasurementFields m={m} set={setM} recommendedPsi={recommendedPsi} minMm={s.minTreadDepthMm} warningMm={s.treadWarningDepthMm} tolerance={s.pressureTolerancePercent} />}
          <WhenFields when={when} setWhen={setWhen} km={km} setKm={setKm} currentKm={currentKm} kmError={errors.odometerKm ?? errors.performedAt}
            showKm={showOdometer ?? (currentKm !== null && currentKm !== undefined)} />
          <Textarea label="Observações" autosize minRows={2} maxLength={1000} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />
          {destination === 'Disposal' && (
            <Group gap="sm">
              <UploadButton label="Anexar documento da baixa" onUploaded={(f) => setFiles((x) => [...x, f])} />
              <AttachmentList files={files} onRemove={(f) => setFiles((x) => x.filter((y) => y.id !== f.id))} />
            </Group>
          )}
          {destination === 'Disposal' && <Alert color="orange" p="xs">A baixa é definitiva: o pneu não poderá ser instalado de novo.</Alert>}
          <Actions onClose={onClose} saving={remove.isPending || swap.isPending} label={replace ? 'Substituir pneu' : 'Remover pneu'}
            color={destination === 'Disposal' ? 'red' : undefined} />
        </Stack>
      </form>
    </OperationModal>
  );
}

// ---------- transfer ----------

export function TransferModal({ opened, onClose, tireId, tireCode }: { opened: boolean; onClose: () => void; tireId: string; tireCode: string }) {
  const transfer = tiresApi.useTransfer(tireId);
  const [kind, setKind] = useState<'vehicles' | 'implements'>('vehicles');
  const [assetId, setAssetId] = useState<string | null>(null);
  const [positionCode, setPositionCode] = useState<string | null>(null);
  const [when, setWhen] = useState<Date | null>(null);
  const [originKm, setOriginKm] = useState<NumberValue>('');
  const [km, setKm] = useState<NumberValue>('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const target = { vehicleId: kind === 'vehicles' ? assetId : null, implementId: kind === 'implements' ? assetId : null };

  const submit = () => {
    if (!assetId || !positionCode) return setErrors({ positionCode: 'Selecione o destino e a posição.' });
    transfer.mutate({ ...target, positionCode, performedAt: iso(when), originOdometerKm: num(originKm), odometerKm: num(km) }, {
      onSuccess: () => { notifySuccess('Pneu transferido.'); onClose(); },
      onError: (e) => setErrors(notifyError(e, 'O pneu não foi transferido').fieldErrors),
    });
  };

  return (
    <OperationModal opened={opened} onClose={onClose} title={`Transferir pneu ${tireCode}`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <Text size="sm" c="dimmed">O pneu sai da posição atual e vai direto para a nova, numa única operação. Os dois registros ficam no histórico.</Text>
          <AssetAndPosition kind={kind} setKind={setKind} assetId={assetId} setAssetId={setAssetId} positionCode={positionCode} setPositionCode={setPositionCode}
            error={errors.positionCode ?? errors.vehicleId} onlyFree />
          <CompatibilityNote tireId={tireId} target={target} positionCode={positionCode} />
          <DateTimePicker label="Quando" placeholder="Agora" valueFormat="DD/MM/YYYY HH:mm" clearable maxDate={new Date()} value={when} onChange={setWhen} />
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <NumberInput label="Hodômetro do veículo de origem" suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
              value={originKm} onChange={setOriginKm} error={errors.originOdometerKm} description="Opcional." />
            {kind === 'vehicles' && (
              <NumberInput label="Hodômetro do veículo de destino" suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
                value={km} onChange={setKm} error={errors.odometerKm} description="Opcional." />
            )}
          </SimpleGrid>
          <Actions onClose={onClose} saving={transfer.isPending} label="Transferir pneu" />
        </Stack>
      </form>
    </OperationModal>
  );
}

// ---------- rotation (seções 14, 67) ----------

export function RotationModal({ opened, onClose, asset }: { opened: boolean; onClose: () => void; asset: AssetTires }) {
  const rotate = tireOperationsApi.useRotate();
  const installed = asset.positions.filter((p) => p.tire);
  const [targets, setTargets] = useState<Record<string, string>>({});
  const [when, setWhen] = useState<Date | null>(null);
  const [km, setKm] = useState<NumberValue>('');
  const [reason, setReason] = useState('');

  useEffect(() => {
    if (opened) { setTargets(Object.fromEntries(installed.map((p) => [p.tire!.tireId, p.position.code]))); setWhen(null); setKm(''); setReason(''); }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the modal opens
  }, [opened]);

  const current = Object.fromEntries(installed.map((p) => [p.position.code, p.tire!.tireId]));
  const moves = installed.filter((p) => targets[p.tire!.tireId] && targets[p.tire!.tireId] !== p.position.code)
    .map((p) => ({ tireId: p.tire!.tireId, toPositionCode: targets[p.tire!.tireId] }));
  const problems = rotationProblems(current, moves);
  const options = asset.positions.map((p) => ({ value: p.position.code, label: `${p.position.code} · ${p.position.label}` }));

  const submit = () =>
    rotate.mutate({
      vehicleId: asset.vehicleId, implementId: asset.implementId, performedAt: iso(when), odometerKm: num(km), reason: reason || null, moves,
    }, {
      onSuccess: () => { notifySuccess(`Rodízio de ${moves.length} pneu(s) registrado.`); onClose(); },
      onError: (e) => notifyError(e, 'O rodízio não foi registrado'),
    });

  return (
    <OperationModal opened={opened} onClose={onClose} title={`Rodízio de pneus · ${asset.assetLabel}`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <Text size="sm" c="dimmed">
            Escolha a nova posição de cada pneu. Tudo é gravado de uma vez: ou o rodízio inteiro é registrado, ou nada muda.
          </Text>
          <Table.ScrollContainer minWidth={420}>
            <Table verticalSpacing="xs">
              <Table.Thead><Table.Tr><Table.Th>Pneu</Table.Th><Table.Th>Posição atual</Table.Th><Table.Th>Nova posição</Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {installed.map((p) => (
                  <Table.Tr key={p.tire!.tireId}>
                    <Table.Td><Text size="sm" fw={600}>{p.tire!.code}</Text><Text size="xs" c="dimmed">{formatTread(p.tire!.currentTreadDepthMm)}</Text></Table.Td>
                    <Table.Td><Text size="sm">{p.position.code}</Text></Table.Td>
                    <Table.Td>
                      <Select aria-label={`Nova posição de ${p.tire!.code}`} data={options} value={targets[p.tire!.tireId] ?? p.position.code} allowDeselect={false}
                        onChange={(v) => setTargets((t) => ({ ...t, [p.tire!.tireId]: v ?? p.position.code }))} />
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {problems.length > 0 && <Alert color="red" p="xs" icon={<IconAlertTriangle size={16} />}>{problems.map((x) => <Text key={x} size="sm">{x}</Text>)}</Alert>}
          {moves.length > 0 && problems.length === 0 && <Text size="sm">{moves.length} pneu(s) mudam de posição.</Text>}
          <WhenFields when={when} setWhen={setWhen} km={km} setKm={setKm} currentKm={asset.currentOdometerKm} showKm={!!asset.vehicleId} />
          <TextInput label="Motivo (opcional)" placeholder="Ex.: rodízio preventivo a cada 20.000 km" maxLength={300} value={reason} onChange={(e) => setReason(e.currentTarget.value)} />
          <Actions onClose={onClose} saving={rotate.isPending} label="Registrar rodízio" disabled={moves.length === 0 || problems.length > 0} />
        </Stack>
      </form>
    </OperationModal>
  );
}

// ---------- inspection (seções 16–21, 68) ----------

export function InspectionModal({ opened, onClose, tireId, tireCode, positionLabel, recommendedPsi, currentKm, showOdometer, occurrenceId }: {
  opened: boolean; onClose: () => void; tireId: string; tireCode: string; positionLabel?: string | null; recommendedPsi?: number | null;
  currentKm?: number | null; showOdometer?: boolean; occurrenceId?: string | null;
}) {
  const settings = tireSettingsApi.useGet();
  const inspect = tiresApi.useInspect(tireId);
  const [m, setM] = useState<Measurement>(emptyMeasurement());
  const [when, setWhen] = useState<Date | null>(null);
  const [km, setKm] = useState<NumberValue>('');
  const [notes, setNotes] = useState('');
  const [photos, setPhotos] = useState<FileInfo[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const s = settings.data;

  useEffect(() => {
    if (opened) { setM({ ...emptyMeasurement(s?.pressureUnit), wear: 'Normal', condition: 'Good' }); setWhen(null); setKm(''); setNotes(''); setPhotos([]); setErrors({}); }
  }, [opened, s?.pressureUnit]);

  const submit = () =>
    inspect.mutate({
      inspectedAt: iso(when), odometerKm: num(km), treadDepthMm: num(m.tread), pressure: num(m.pressure), pressureUnit: num(m.pressure) !== null ? m.unit : null,
      condition: m.condition ?? 'Good', wearPattern: m.wear, damages: m.damages, notes: notes || null, fileIds: photos.map((p) => p.id), occurrenceId,
    }, {
      onSuccess: (i) => {
        notifySuccess(i.requiresAction ? 'Inspeção registrada. O pneu ficou com alerta para acompanhamento.' : 'Inspeção registrada.');
        onClose();
      },
      onError: (e) => setErrors(notifyError(e, 'A inspeção não foi registrada').fieldErrors),
    });

  return (
    <OperationModal opened={opened} onClose={onClose} title={`Inspecionar pneu ${tireCode}${positionLabel ? ` · ${positionLabel}` : ''}`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          {s && <MeasurementFields m={m} set={setM} recommendedPsi={recommendedPsi} minMm={s.minTreadDepthMm} warningMm={s.treadWarningDepthMm}
            tolerance={s.pressureTolerancePercent} requireCondition />}
          {(errors.treadDepthMm || errors.pressure) && <Text size="sm" c="red">{errors.treadDepthMm ?? errors.pressure}</Text>}
          <Textarea label="Observações" autosize minRows={2} maxLength={2000} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} error={errors.notes}
            placeholder="Ex.: corte de 2 cm na lateral externa" />
          <Group gap="sm" align="flex-start">
            <UploadButton camera label="Tirar foto" onUploaded={(f) => setPhotos((x) => [...x, f])} />
            <UploadButton label="Anexar arquivo" onUploaded={(f) => setPhotos((x) => [...x, f])} />
          </Group>
          {photos.length > 0 && <AttachmentList files={photos} onRemove={(f) => setPhotos((x) => x.filter((y) => y.id !== f.id))} />}
          <WhenFields when={when} setWhen={setWhen} km={km} setKm={setKm} currentKm={currentKm} kmError={errors.odometerKm ?? errors.inspectedAt}
            showKm={showOdometer ?? (currentKm !== null && currentKm !== undefined)} />
          <Actions onClose={onClose} saving={inspect.isPending} label="Salvar inspeção" />
        </Stack>
      </form>
    </OperationModal>
  );
}

// ---------- repair / retread (seções 22, 23) ----------

export function SendToServiceModal({ opened, onClose, tireId, tireCode, kind, inPlace, canSeeCosts }: {
  opened: boolean; onClose: () => void; tireId: string; tireCode: string; kind: ServiceKind; inPlace?: boolean; canSeeCosts: boolean;
}) {
  const send = tiresApi.useSendToService(tireId);
  const [workshopId, setWorkshopId] = useState<string | null>(null);
  const [providerName, setProviderName] = useState('');
  const [repairType, setRepairType] = useState<string | null>(null);
  const [description, setDescription] = useState('');
  const [cost, setCost] = useState<NumberValue>('');
  const [errors, setErrors] = useState<Record<string, string>>({});

  const submit = () =>
    send.mutate({
      kind, workshopId, providerName: providerName || null, repairType, description: description || null,
      cost: inPlace && canSeeCosts ? num(cost) : null, resultNotes: inPlace ? description || null : null,
    }, {
      onSuccess: () => { notifySuccess(inPlace ? 'Conserto registrado.' : kind === 'Retread' ? 'Pneu enviado para recapagem.' : 'Pneu enviado para conserto.'); onClose(); },
      onError: (e) => setErrors(notifyError(e, 'O serviço não foi registrado').fieldErrors),
    });

  return (
    <OperationModal opened={opened} onClose={onClose}
      title={inPlace ? `Conserto no veículo · ${tireCode}` : `${kind === 'Retread' ? 'Enviar para recapagem' : 'Enviar para conserto'} · ${tireCode}`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          {inPlace && <Text size="sm" c="dimmed">Para consertos feitos sem tirar o pneu do veículo (ex.: furo). O pneu continua na posição.</Text>}
          {kind === 'Retread' && <Text size="sm" c="dimmed">A decisão de recapar é do fornecedor: ao concluir, registre se a carcaça foi aprovada ou reprovada.</Text>}
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <WorkshopPicker label="Fornecedor (cadastrado)" value={workshopId} onChange={setWorkshopId} error={errors.workshopId} />
            <TextInput label="ou nome do fornecedor" maxLength={120} value={providerName} onChange={(e) => setProviderName(e.currentTarget.value)} />
          </SimpleGrid>
          {kind === 'Repair' && <Select label="Tipo de conserto" data={toSelectData(REPAIR_TYPE)} value={repairType} onChange={setRepairType} clearable />}
          <Textarea label={inPlace ? 'O que foi feito' : 'Descrição'} autosize minRows={2} maxLength={1000} value={description} onChange={(e) => setDescription(e.currentTarget.value)} />
          {inPlace && canSeeCosts && <NumberInput label="Valor" prefix="R$ " decimalScale={2} fixedDecimalScale thousandSeparator="." decimalSeparator="," allowNegative={false} value={cost} onChange={setCost} error={errors.cost} />}
          <Actions onClose={onClose} saving={send.isPending} label={inPlace ? 'Registrar conserto' : 'Enviar'} />
        </Stack>
      </form>
    </OperationModal>
  );
}

export function CompleteServiceModal({ opened, onClose, order, canSeeCosts }: { opened: boolean; onClose: () => void; order: TireServiceOrder; canSeeCosts: boolean }) {
  const complete = tireOperationsApi.useCompleteService(order.id);
  const [result, setResult] = useState<'Approved' | 'Rejected'>('Approved');
  const [treadPattern, setTreadPattern] = useState('');
  const [newTread, setNewTread] = useState<NumberValue>('');
  const [cost, setCost] = useState<NumberValue>('');
  const [warranty, setWarranty] = useState<Date | null>(null);
  const [notes, setNotes] = useState('');
  const [storage, setStorage] = useState('');
  const [files, setFiles] = useState<FileInfo[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const retread = order.kind === 'Retread';

  const submit = () =>
    complete.mutate({
      result, treadPattern: treadPattern || null, newTreadDepthMm: num(newTread), cost: canSeeCosts ? num(cost) : null,
      warrantyUntil: warranty ? dayjs(warranty).format('YYYY-MM-DD') : null, resultNotes: notes || null, storageLocation: storage || null, fileIds: files.map((f) => f.id),
    }, {
      onSuccess: () => { notifySuccess(result === 'Approved' ? 'Serviço concluído. O pneu voltou ao estoque.' : 'Serviço reprovado. O pneu aguarda decisão.'); onClose(); },
      onError: (e) => setErrors(notifyError(e, 'O serviço não foi concluído').fieldErrors),
    });

  return (
    <OperationModal opened={opened} onClose={onClose} title={`Concluir ${retread ? `recapagem nº ${order.retreadNumber}` : 'conserto'} · ${order.tireCode}`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <Radio.Group label="Resultado" withAsterisk value={result} onChange={(v) => setResult(v as 'Approved' | 'Rejected')}>
            <Group mt={4}>
              <Radio value="Approved" label={retread ? 'Aprovado — recapado' : 'Aprovado — consertado'} />
              <Radio value="Rejected" label={retread ? 'Reprovado — carcaça sem condição' : 'Reprovado — sem conserto'} />
            </Group>
          </Radio.Group>
          {retread && result === 'Approved' && (
            <SimpleGrid cols={{ base: 1, sm: 2 }}>
              <NumberInput label="Sulco da banda nova (mm)" withAsterisk decimalScale={1} decimalSeparator="," allowNegative={false} value={newTread} onChange={setNewTread} error={errors.newTreadDepthMm} />
              <TextInput label="Banda aplicada" placeholder="Ex.: BDR 240" maxLength={80} value={treadPattern} onChange={(e) => setTreadPattern(e.currentTarget.value)} />
            </SimpleGrid>
          )}
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            {canSeeCosts && <NumberInput label="Valor" prefix="R$ " decimalScale={2} fixedDecimalScale thousandSeparator="." decimalSeparator="," allowNegative={false} value={cost} onChange={setCost} error={errors.cost} />}
            {result === 'Approved' && <DateInput label="Garantia até" valueFormat="DD/MM/YYYY" clearable value={warranty} onChange={setWarranty} />}
            {result === 'Approved' && <TextInput label="Local de armazenamento" maxLength={120} value={storage} onChange={(e) => setStorage(e.currentTarget.value)} />}
          </SimpleGrid>
          <Textarea label={result === 'Rejected' ? 'Motivo da reprovação' : 'Observações'} withAsterisk={result === 'Rejected'} autosize minRows={2} maxLength={1000}
            value={notes} onChange={(e) => setNotes(e.currentTarget.value)} error={errors.resultNotes} />
          <Group gap="sm"><UploadButton label="Anexar nota / laudo" onUploaded={(f) => setFiles((x) => [...x, f])} /></Group>
          {files.length > 0 && <AttachmentList files={files} onRemove={(f) => setFiles((x) => x.filter((y) => y.id !== f.id))} />}
          <Actions onClose={onClose} saving={complete.isPending} label="Concluir serviço" />
        </Stack>
      </form>
    </OperationModal>
  );
}

// ---------- disposal, stock, cost, correction, reasons ----------

export function DisposeModal({ opened, onClose, tireId, tireCode }: { opened: boolean; onClose: () => void; tireId: string; tireCode: string }) {
  const dispose = tiresApi.useDispose(tireId);
  const [reason, setReason] = useState<string | null>(null);
  const [destination, setDestination] = useState('');
  const [notes, setNotes] = useState('');
  const [files, setFiles] = useState<FileInfo[]>([]);
  const [error, setError] = useState<string | null>(null);

  const submit = () => {
    if (!reason) return setError('Informe o motivo da baixa.');
    dispose.mutate({ reason, destination: destination || null, notes: notes || null, fileIds: files.map((f) => f.id) }, {
      onSuccess: () => { notifySuccess('Baixa registrada.'); onClose(); },
      onError: (e) => notifyError(e, 'A baixa não foi registrada'),
    });
  };

  return (
    <OperationModal opened={opened} onClose={onClose} title={`Dar baixa no pneu ${tireCode}?`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <Alert color="orange" p="xs">A baixa é definitiva. O pneu sai do estoque e o histórico completo dele é mantido.</Alert>
          <Select label="Motivo" withAsterisk data={toSelectData(DISPOSAL_REASON)} value={reason} onChange={(v) => { setReason(v); setError(null); }} error={error} />
          <TextInput label="Destino" placeholder="Ex.: reciclagem, vendido para…" maxLength={500} value={destination} onChange={(e) => setDestination(e.currentTarget.value)} />
          <Textarea label="Condição / observações" autosize minRows={2} maxLength={500} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />
          <Group gap="sm"><UploadButton label="Anexar documento" onUploaded={(f) => setFiles((x) => [...x, f])} /></Group>
          {files.length > 0 && <AttachmentList files={files} onRemove={(f) => setFiles((x) => x.filter((y) => y.id !== f.id))} />}
          <Actions onClose={onClose} saving={dispose.isPending} label="Dar baixa" color="red" />
        </Stack>
      </form>
    </OperationModal>
  );
}

export function StockModal({ opened, onClose, tireId, tireCode, mode }: { opened: boolean; onClose: () => void; tireId: string; tireCode: string; mode: 'evaluation' | 'return' }) {
  const evaluation = tiresApi.useEvaluation(tireId);
  const back = tiresApi.useReturnToStock(tireId);
  const mutation = mode === 'evaluation' ? evaluation : back;
  const [storage, setStorage] = useState('');
  const [notes, setNotes] = useState('');
  const submit = () => mutation.mutate({ storageLocation: storage || null, notes: notes || null }, {
    onSuccess: () => { notifySuccess(mode === 'evaluation' ? 'Pneu separado para avaliação.' : 'Pneu liberado e de volta ao estoque.'); onClose(); },
    onError: (e) => notifyError(e, 'A operação não foi registrada'),
  });
  return (
    <OperationModal opened={opened} onClose={onClose} title={`${mode === 'evaluation' ? 'Separar para avaliação' : 'Liberar para uso'} · ${tireCode}`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <TextInput label="Local de armazenamento" placeholder="Ex.: Almoxarifado A · Prateleira 3" maxLength={120} value={storage} onChange={(e) => setStorage(e.currentTarget.value)} />
          <Textarea label={mode === 'return' ? 'Resultado da avaliação' : 'Motivo'} autosize minRows={2} maxLength={1000} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />
          <Actions onClose={onClose} saving={mutation.isPending} label={mode === 'evaluation' ? 'Separar' : 'Liberar para uso'} />
        </Stack>
      </form>
    </OperationModal>
  );
}

export function CostModal({ opened, onClose, tireId }: { opened: boolean; onClose: () => void; tireId: string }) {
  const add = tiresApi.useAddCost(tireId);
  const [type, setType] = useState<string>('Installation');
  const [date, setDate] = useState<Date | null>(new Date());
  const [amount, setAmount] = useState<NumberValue>('');
  const [description, setDescription] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const submit = () => {
    if (num(amount) === null) return setErrors({ amount: 'Informe o valor.' });
    add.mutate({ type, incurredOn: dayjs(date ?? new Date()).format('YYYY-MM-DD'), amount: num(amount)!, description: description || null }, {
      onSuccess: () => { notifySuccess('Custo registrado.'); onClose(); },
      onError: (e) => setErrors(notifyError(e, 'O custo não foi registrado').fieldErrors),
    });
  };
  return (
    <OperationModal opened={opened} onClose={onClose} title="Registrar custo do pneu">
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <Text size="sm" c="dimmed">Consertos e recapagens entram sozinhos ao concluir o serviço. Aqui: montagem, balanceamento e outros.</Text>
          <SegmentedControl value={type} onChange={setType} data={[{ value: 'Installation', label: COST_TYPE.Installation.label }, { value: 'Other', label: COST_TYPE.Other.label }]} />
          <SimpleGrid cols={2}>
            <DateInput label="Data" withAsterisk valueFormat="DD/MM/YYYY" maxDate={new Date()} value={date} onChange={setDate} error={errors.incurredOn} />
            <NumberInput label="Valor" withAsterisk prefix="R$ " decimalScale={2} fixedDecimalScale thousandSeparator="." decimalSeparator="," allowNegative={false} value={amount} onChange={setAmount} error={errors.amount} />
          </SimpleGrid>
          <TextInput label="Descrição" maxLength={300} value={description} onChange={(e) => setDescription(e.currentTarget.value)} />
          <Actions onClose={onClose} saving={add.isPending} label="Registrar custo" />
        </Stack>
      </form>
    </OperationModal>
  );
}

export function CorrectionModal({ opened, onClose, installation }: { opened: boolean; onClose: () => void; installation: TireInstallation }) {
  const correct = tireOperationsApi.useCorrectInstallation(installation.id);
  const [installed, setInstalled] = useState<NumberValue>(installation.installedOdometerKm ?? '');
  const [removed, setRemoved] = useState<NumberValue>(installation.removedOdometerKm ?? '');
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const submit = () => {
    if (!reason.trim()) return setErrors({ reason: 'Informe o motivo da correção.' });
    correct.mutate({ installedOdometerKm: num(installed), removedOdometerKm: installation.removedAt ? num(removed) : null, reason: reason.trim() }, {
      onSuccess: () => { notifySuccess('Histórico corrigido. A correção ficou registrada.'); onClose(); },
      onError: (e) => setErrors(notifyError(e, 'A correção não foi registrada').fieldErrors),
    });
  };
  return (
    <OperationModal opened={opened} onClose={onClose} title={`Corrigir km · ${installation.assetLabel}, ${installation.positionLabel}`}>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <Text size="sm" c="dimmed">Só o km pode ser corrigido. Datas e posições não mudam; a correção aparece no histórico com o valor anterior.</Text>
          <SimpleGrid cols={2}>
            <NumberInput label="Km na instalação" suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false} value={installed} onChange={setInstalled} error={errors.installedOdometerKm} />
            {installation.removedAt && <NumberInput label="Km na remoção" suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false} value={removed} onChange={setRemoved} error={errors.removedOdometerKm} />}
          </SimpleGrid>
          <Textarea label="Motivo da correção" withAsterisk autosize minRows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.currentTarget.value)} error={errors.reason} />
          <Actions onClose={onClose} saving={correct.isPending} label="Corrigir" />
        </Stack>
      </form>
    </OperationModal>
  );
}

/** Text-only confirmation (cancel a service, review an anomaly). */
export function ReasonModal({ opened, onClose, title, description, label, placeholder, confirm, color, saving, onSubmit }: {
  opened: boolean; onClose: () => void; title: string; description: string; label: string; placeholder?: string; confirm: string; color?: string;
  saving: boolean; onSubmit: (text: string, done: () => void) => void;
}) {
  const [text, setText] = useState('');
  const [error, setError] = useState<string | null>(null);
  useEffect(() => { if (opened) { setText(''); setError(null); } }, [opened]);
  return (
    <Modal opened={opened} onClose={onClose} centered title={title}>
      <form onSubmit={(e) => { e.preventDefault(); if (!text.trim()) { setError('Campo obrigatório.'); return; } onSubmit(text.trim(), onClose); }}>
        <Stack>
          <Text size="sm">{description}</Text>
          <Textarea label={label} withAsterisk autosize minRows={3} maxLength={1000} placeholder={placeholder} value={text}
            onChange={(e) => { setText(e.currentTarget.value); setError(null); }} error={error} data-autofocus />
          <Actions onClose={onClose} saving={saving} label={confirm} color={color} />
        </Stack>
      </form>
    </Modal>
  );
}
