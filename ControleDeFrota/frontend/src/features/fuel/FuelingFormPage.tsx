import { useEffect, useMemo, useState } from 'react';
import { Alert, Anchor, Group, NumberInput, Paper, Select, Stack, Switch, Text, Textarea, TextInput } from '@mantine/core';
import { DateTimePicker } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { IconAlertTriangle, IconGasStation } from '@tabler/icons-react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { notifications } from '@mantine/notifications';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { DriverPicker, VehiclePicker } from '../../components/EntityPickers';
import { FormActions, FormSection, useUnsavedChangesGuard } from '../../components/forms';
import { PageHeader } from '../../components/PageHeader';
import { submitWithFeedback } from '../../components/useApiForm';
import { formatCurrency, formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { exceedsTank, FUEL_UNIT_SYMBOL, fuelingTotal, formatUnitPrice } from '../../lib/fuel';
import { odometerHint } from '../../lib/mileage';
import type { FileInfo } from '../operations/api';
import { fuelSettingsApi, fuelStationsApi, fuelTypesApi, fuelingsApi } from './api';
import { PAYMENT_METHOD, type Fueling, type PaymentMethod } from './fuel';

type NumberValue = number | string;
const toNumber = (v: NumberValue) => (v === '' || v === null ? null : Number(v));

/** /abastecimentos/novo (quick entry) and /abastecimentos/:id/corrigir (audited correction) — one form, two modes. */
export function FuelingFormPage() {
  const { id } = useParams();
  const detail = fuelingsApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(f) => <FuelingForm fueling={f} />}</EntityFormPage>;
}

function FuelingForm({ fueling }: { fueling?: Fueling }) {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { can } = useAuth();
  const isCorrection = !!fueling;
  const create = fuelingsApi.useCreate();
  const correct = fuelingsApi.useCorrect(fueling?.id ?? '');
  const types = fuelTypesApi.useList();
  const stations = fuelStationsApi.useList({ isActive: 'true', pageSize: 100, sortBy: 'name' });
  const settings = fuelSettingsApi.useGet();
  const [files, setFiles] = useState<FileInfo[]>([]);

  const initialValues = {
    vehicleId: fueling?.vehicleId ?? params.get('veiculo'),
    driverId: fueling?.driverId ?? null,
    fuelStationId: fueling?.fuelStationId ?? null,
    fuelTypeId: fueling?.fuelTypeId ?? null,
    fueledAt: fueling ? new Date(fueling.fueledAt) : new Date(),
    odometerKm: (fueling?.odometerKm ?? '') as NumberValue,
    quantity: (fueling?.quantity ?? '') as NumberValue,
    unitPrice: (fueling?.unitPrice ?? '') as NumberValue,
    isFullTank: fueling?.isFullTank ?? true,
    paymentMethod: (fueling?.paymentMethod ?? 'FuelCard') as PaymentMethod | null,
    receiptNumber: fueling?.receiptNumber ?? '',
    notes: fueling?.notes ?? '',
    reason: '',
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validateInputOnBlur: true,
    validate: {
      vehicleId: (v) => (v ? null : 'Veículo: campo obrigatório.'),
      fuelTypeId: (v) => (v ? null : 'Combustível: campo obrigatório.'),
      odometerKm: (v) => (v === '' ? 'Hodômetro: informe a quilometragem no momento do abastecimento.' : null),
      quantity: (v) => (v === '' || Number(v) <= 0 ? 'A quantidade deve ser maior que zero.' : null),
      unitPrice: (v) => (v === '' || Number(v) <= 0 ? 'O preço deve ser maior que zero.' : null),
      paymentMethod: (v) => (v ? null : 'Forma de pagamento: campo obrigatório.'),
      fueledAt: (v) => (!v ? 'Informe a data e hora.' : v.getTime() > Date.now() + 5 * 60_000 ? 'A data não pode ser futura.' : null),
      reason: (v) => (isCorrection && !v.trim() ? 'Informe o motivo da correção.' : null),
    },
  });
  const guard = useUnsavedChangesGuard(form.isDirty());
  const defaults = fuelingsApi.useFormDefaults(isCorrection ? null : form.values.vehicleId);
  const priceHint = fuelingsApi.usePriceHint(form.values.fuelStationId, form.values.fuelTypeId);

  // Reuse what is known about the vehicle (seção 27) — only facts or the last choices, never the odometer or the price:
  // those are what is being recorded, and a pre-filled value is how wrong records get saved.
  useEffect(() => {
    const d = defaults.data;
    if (!d || isCorrection) return;
    form.setValues((v) => ({
      driverId: v.driverId ?? d.currentDriverId,
      fuelTypeId: v.fuelTypeId ?? d.lastFuelTypeId,
      fuelStationId: v.fuelStationId ?? d.lastFuelStationId,
    }));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- apply once per loaded vehicle
  }, [defaults.data?.vehicleId]);

  const type = types.data?.find((t) => t.id === form.values.fuelTypeId);
  const unit = type?.unit ?? 'Liter';
  const symbol = FUEL_UNIT_SYMBOL[unit];
  const total = fuelingTotal(toNumber(form.values.quantity), toNumber(form.values.unitPrice));
  const km = toNumber(form.values.odometerKm);
  const d = defaults.data;
  const hint = d && km !== null
    ? odometerHint(km, d.currentOdometerKm, d.odometerUpdatedAt ? new Date(d.odometerUpdatedAt) : null, form.values.fueledAt ?? new Date())
    : null;
  const tankWarning = d && exceedsTank(toNumber(form.values.quantity), d.tankCapacity, settings.data?.tankTolerancePercent ?? 5);

  const stationOptions = useMemo(() => {
    const list = (stations.data?.items ?? []).map((s) => ({ value: s.id, label: s.isInternal ? `${s.name} (tanque próprio)` : s.name }));
    // A correction may point at a station that was deactivated meanwhile.
    if (fueling?.fuelStationId && !list.some((o) => o.value === fueling.fuelStationId))
      list.unshift({ value: fueling.fuelStationId, label: fueling.fuelStationName ?? 'Posto' });
    return list;
  }, [stations.data, fueling]);
  const typeOptions = (types.data ?? []).map((t) => ({ value: t.id, label: t.name }));
  if (fueling && !typeOptions.some((o) => o.value === fueling.fuelTypeId)) typeOptions.unshift({ value: fueling.fuelTypeId, label: fueling.fuelTypeName });

  const toFields = (v: typeof initialValues) => ({
    driverId: v.driverId,
    fuelStationId: v.fuelStationId,
    fuelTypeId: v.fuelTypeId,
    fueledAt: v.fueledAt ? v.fueledAt.toISOString() : null,
    odometerKm: toNumber(v.odometerKm),
    quantity: toNumber(v.quantity),
    unitPrice: toNumber(v.unitPrice),
    totalAmount: null,
    isFullTank: v.isFullTank,
    paymentMethod: v.paymentMethod,
    receiptNumber: v.receiptNumber.trim() || null,
    notes: v.notes.trim() || null,
  });

  const afterSave = (saved: Fueling) => {
    guard.release();
    if (saved.status === 'PendingReview')
      notifications.show({ color: 'orange', title: 'Abastecimento salvo com alerta', message: 'Ele foi registrado e ficou marcado para revisão. Veja o motivo na tela do abastecimento.', autoClose: 7000 });
    navigate(`/abastecimentos/${saved.id}`);
  };

  const handleSubmit = isCorrection
    ? submitWithFeedback({
      form, mutation: correct, successMessage: 'Correção registrada com sucesso.', errorTitle: 'A correção não foi salva', onSuccess: afterSave,
      toBody: (v) => ({ ...toFields(v), reason: v.reason.trim() }),
    })
    : submitWithFeedback({
      form, mutation: create, successMessage: 'Abastecimento registrado com sucesso.', errorTitle: 'O abastecimento não foi salvo', onSuccess: afterSave,
      toBody: (v) => ({ ...toFields(v), vehicleId: v.vehicleId, fileIds: files.map((f) => f.id) }),
    });

  const saving = create.isPending || correct.isPending;
  const title = isCorrection ? 'Corrigir abastecimento' : 'Registrar abastecimento';

  return (
    <>
      <PageHeader title={title}
        description={isCorrection ? 'A correção fica registrada com o motivo, quem corrigiu e os valores anteriores.' : 'Veículo, hodômetro, combustível, quantidade e preço — o total é calculado.'}
        breadcrumbs={[{ label: 'Abastecimentos', to: '/abastecimentos' }, { label: isCorrection ? 'Corrigir' : 'Novo' }]} />
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md" maw={880}>
          <FormSection title="Abastecimento" cols={2}>
            {isCorrection ? (
              <TextInput label="Veículo" value={formatPlate(fueling.licensePlate)} readOnly description="Para trocar o veículo, cancele e registre de novo." />
            ) : (
              <VehiclePicker label="Veículo" withAsterisk size="md" value={form.values.vehicleId} error={form.errors.vehicleId}
                onChange={(v) => form.setValues({ vehicleId: v, driverId: null, fuelTypeId: null, fuelStationId: null })} />
            )}
            <div>
              <NumberInput label="Hodômetro" withAsterisk size="md" suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
                inputMode="numeric" description={d ? `Último: ${formatNumber(d.currentOdometerKm)} km${d.odometerUpdatedAt ? ` em ${formatDateTime(d.odometerUpdatedAt)}` : ''}` : undefined}
                {...form.getInputProps('odometerKm')} />
              {hint?.kind === 'decrease' && <Text size="xs" c="red" mt={4}>Menor que a última leitura ({formatNumber(hint.previousKm)} km). O registro será recusado.</Text>}
              {hint?.kind === 'suspicious' && <Text size="xs" c="orange" mt={4}>+{formatNumber(hint.deltaKm)} km parece muito. Se estiver certo, o abastecimento será salvo e ficará para revisão.</Text>}
              {hint?.kind === 'increase' && <Text size="xs" c="dimmed" mt={4}>+{formatNumber(hint.deltaKm)} km desde a última leitura.</Text>}
            </div>
            <Select label="Combustível" withAsterisk size="md" data={typeOptions} searchable {...form.getInputProps('fuelTypeId')}
              description={d?.lastFuelTypeId && form.values.fuelTypeId === d.lastFuelTypeId && !isCorrection ? 'O mesmo do último abastecimento' : undefined} />
            <div>
              <NumberInput label="Quantidade" withAsterisk size="md" suffix={` ${symbol}`} decimalSeparator="," thousandSeparator="." decimalScale={3} allowNegative={false}
                inputMode="decimal" {...form.getInputProps('quantity')} />
              {tankWarning && <Text size="xs" c="orange" mt={4}>Acima da capacidade cadastrada ({formatNumber(d.tankCapacity)} {symbol}). Será salvo e ficará para revisão.</Text>}
            </div>
            <div>
              <NumberInput label={`Preço por ${symbol}`} withAsterisk size="md" prefix="R$ " decimalSeparator="," thousandSeparator="." decimalScale={4} allowNegative={false}
                inputMode="decimal" {...form.getInputProps('unitPrice')} />
              {priceHint.data && (
                <Text size="xs" c="dimmed" mt={4}>
                  {priceHint.data.source === 'Reference' ? 'Preço de referência do posto' : 'Último preço pago neste posto'}: {formatUnitPrice(priceHint.data.price, unit)}{' '}
                  <Anchor component="button" type="button" size="xs" onClick={() => form.setFieldValue('unitPrice', priceHint.data!.price)}>usar</Anchor>
                </Text>
              )}
            </div>
            <Paper p="sm" withBorder={false} bg="var(--mantine-color-default-hover)">
              <Text size="xs" c="dimmed" fw={500} tt="uppercase">Total calculado</Text>
              <Text fw={700} fz={26} aria-live="polite">{total === null ? '—' : formatCurrency(total)}</Text>
              <Text size="xs" c="dimmed">quantidade × preço</Text>
            </Paper>
          </FormSection>

          <FormSection title="Onde, quem e quando" cols={2}>
            <Select label="Posto" size="md" data={stationOptions} searchable clearable allowDeselect placeholder="Selecione o posto"
              description={!isCorrection && d?.lastFuelStationId && form.values.fuelStationId === d.lastFuelStationId ? 'Último posto usado — confira' : undefined}
              nothingFoundMessage={can(PERMISSIONS.fuel.managestations) ? 'Nenhum posto. Cadastre em Postos de combustível.' : 'Nenhum posto encontrado'}
              {...form.getInputProps('fuelStationId')} />
            <DriverPicker label="Motorista" size="md" withAsterisk={d?.requireDriver} value={form.values.driverId} error={form.errors.driverId}
              initialLabel={fueling?.driverName ?? (form.values.driverId === d?.currentDriverId ? d?.currentDriverName : null)}
              description={!isCorrection && d?.currentDriverId && form.values.driverId === d.currentDriverId ? 'Motorista alocado ao veículo' : undefined}
              onChange={(v) => form.setFieldValue('driverId', v)} />
            <DateTimePicker label="Data e hora" withAsterisk size="md" valueFormat="DD/MM/YYYY HH:mm" maxDate={new Date()}
              description={isCorrection && fueling.odometerReadingStatus ? 'Não muda: este abastecimento já gerou uma leitura de hodômetro' : undefined}
              disabled={isCorrection && !!fueling.odometerReadingStatus && fueling.odometerReadingStatus !== 'Rejected'}
              {...form.getInputProps('fueledAt')} />
            <Switch label="Tanque cheio" size="md" description="Desligue se foi só um complemento. O consumo é calculado de tanque cheio a tanque cheio."
              {...form.getInputProps('isFullTank', { type: 'checkbox' })} />
            <Select label="Forma de pagamento" withAsterisk size="md" data={toSelectData(PAYMENT_METHOD)} {...form.getInputProps('paymentMethod')} />
            <TextInput label="Número do cupom / nota" size="md" maxLength={60} {...form.getInputProps('receiptNumber')} />
          </FormSection>

          {!isCorrection && (
            <FormSection title="Comprovante" description="Foto do cupom, nota fiscal ou outro comprovante (PDF, JPG ou PNG)." cols={1}>
              <Group gap="sm">
                <UploadButton camera label="Fotografar cupom" onUploaded={(f) => setFiles((x) => [...x, f])} />
                <UploadButton onUploaded={(f) => setFiles((x) => [...x, f])} />
              </Group>
              <AttachmentList files={files} onRemove={(f) => setFiles((x) => x.filter((y) => y.id !== f.id))} />
            </FormSection>
          )}

          <FormSection title={isCorrection ? 'Motivo e observações' : 'Observações'} cols={1}>
            {isCorrection && (
              <Textarea label="Motivo da correção" withAsterisk autosize minRows={2} maxLength={1000}
                placeholder="Ex.: quantidade digitada errada, conferido com o cupom" {...form.getInputProps('reason')} />
            )}
            <Textarea label="Observações" autosize minRows={2} maxLength={1000} {...form.getInputProps('notes')} />
          </FormSection>

          {isCorrection && fueling.odometerReadingStatus === 'Valid' && (
            <Alert color="blue" icon={<IconAlertTriangle size={18} />} p="sm">
              Este abastecimento já atualizou o hodômetro do veículo. Corrigir o km gera uma correção de hodômetro auditada e exige permissão de
              corrigir hodômetro.
            </Alert>
          )}
          {!isCorrection && !form.values.vehicleId && (
            <Alert color="gray" icon={<IconGasStation size={18} />} p="sm">Comece pelo veículo: o sistema mostra o último hodômetro e sugere o combustível.</Alert>
          )}
          <FormActions saving={saving} onCancel={() => navigate(-1)} submitLabel={isCorrection ? 'Salvar correção' : 'Salvar abastecimento'} />
        </Stack>
      </form>
    </>
  );
}
