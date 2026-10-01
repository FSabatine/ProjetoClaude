import { Box, NumberInput, Select, Stack, TextInput, Textarea } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { FormActions, FormSection, MASKS, MaskedInput, useUnsavedChangesGuard } from '../../components/forms';
import { PageHeader } from '../../components/PageHeader';
import { submitWithFeedback } from '../../components/useApiForm';
import { formatPlate, fromApiDate, toApiDate } from '../../lib/format';
import { combine, required, rules } from '../../lib/validators';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import {
  FUEL_TYPE,
  VEHICLE_CATEGORY,
  VEHICLE_STATUS,
  VEHICLE_TYPE,
  vehiclesApi,
  type FuelType,
  type Vehicle,
  type VehicleCategory,
  type VehicleStatus,
  type VehicleType,
} from './vehicles';

const CURRENT_YEAR = new Date().getFullYear();
type NumberValue = number | string;
const toNumber = (v: NumberValue) => (v === '' || v === null ? null : Number(v));

export function VehicleFormPage() {
  const { id } = useParams();
  const detail = vehiclesApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(vehicle) => <VehicleForm vehicle={vehicle} />}</EntityFormPage>;
}

function VehicleForm({ vehicle }: { vehicle?: Vehicle }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const save = vehiclesApi.useSave(vehicle?.id);
  const readOnly = !can(vehicle ? PERMISSIONS.vehicles.update : PERMISSIONS.vehicles.create);

  const initialValues = {
    licensePlate: vehicle ? formatPlate(vehicle.licensePlate) : '',
    renavam: vehicle?.renavam ?? '',
    chassis: vehicle?.chassis ?? '',
    manufacturer: vehicle?.manufacturer ?? '',
    model: vehicle?.model ?? '',
    manufacturingYear: (vehicle?.manufacturingYear ?? CURRENT_YEAR) as NumberValue,
    modelYear: (vehicle?.modelYear ?? CURRENT_YEAR) as NumberValue,
    color: vehicle?.color ?? '',
    type: (vehicle?.type ?? null) as VehicleType | null,
    category: (vehicle?.category ?? null) as VehicleCategory | null,
    fuelType: (vehicle?.fuelType ?? 'DieselS10') as FuelType | null,
    cargoCapacityKg: (vehicle?.cargoCapacityKg ?? '') as NumberValue,
    tareWeightKg: (vehicle?.tareWeightKg ?? '') as NumberValue,
    currentOdometerKm: (vehicle?.currentOdometerKm ?? 0) as NumberValue,
    hourMeter: (vehicle?.hourMeter ?? '') as NumberValue,
    status: (vehicle?.status ?? 'Available') as VehicleStatus,
    acquisitionDate: fromApiDate(vehicle?.acquisitionDate),
    acquisitionValue: (vehicle?.acquisitionValue ?? '') as NumberValue,
    notes: vehicle?.notes ?? '',
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validateInputOnBlur: true,
    validate: {
      licensePlate: combine(required('Placa'), rules.plate),
      renavam: combine(required('RENAVAM'), rules.renavam),
      chassis: combine(required('Chassi'), rules.chassis),
      manufacturer: required('Fabricante'),
      model: required('Modelo'),
      manufacturingYear: (v) => (v === '' ? 'Ano de fabricação: campo obrigatório.' : Number(v) < 1950 || Number(v) > CURRENT_YEAR + 1 ? `Informe um ano entre 1950 e ${CURRENT_YEAR + 1}.` : null),
      modelYear: (v, values) =>
        v === '' ? 'Ano do modelo: campo obrigatório.'
          : Number(v) !== Number(values.manufacturingYear) && Number(v) !== Number(values.manufacturingYear) + 1
            ? 'Deve ser igual ao ano de fabricação ou o ano seguinte.' : null,
      type: (v) => (v ? null : 'Tipo: campo obrigatório.'),
      fuelType: (v) => (v ? null : 'Combustível: campo obrigatório.'),
      currentOdometerKm: (v) => (v === '' ? 'Hodômetro: campo obrigatório (informe 0 para veículo novo).' : null),
      acquisitionDate: (v) => (v && v > new Date() ? 'A data de aquisição não pode ser futura.' : null),
    },
  });

  const guard = useUnsavedChangesGuard(form.isDirty());

  const handleSubmit = submitWithFeedback({
    form,
    mutation: save,
    successMessage: vehicle ? 'Veículo atualizado com sucesso.' : 'Veículo cadastrado com sucesso.',
    errorTitle: 'O veículo não foi salvo',
    onSuccess: (saved) => {
      guard.release();
      navigate(`/veiculos/${saved.id}`);
    },
    toBody: (v) => ({
      ...v,
      manufacturingYear: toNumber(v.manufacturingYear),
      modelYear: toNumber(v.modelYear),
      color: v.color || null,
      cargoCapacityKg: toNumber(v.cargoCapacityKg),
      tareWeightKg: toNumber(v.tareWeightKg),
      // ADR-019: after registration the odometer only changes through readings (Quilometragem tab).
      currentOdometerKm: vehicle ? null : toNumber(v.currentOdometerKm),
      hourMeter: toNumber(v.hourMeter),
      acquisitionDate: toApiDate(v.acquisitionDate),
      acquisitionValue: toNumber(v.acquisitionValue),
      notes: v.notes || null,
    }),
  });

  const title = vehicle ? `${formatPlate(vehicle.licensePlate)} · ${vehicle.manufacturer} ${vehicle.model}` : 'Novo veículo';

  return (
    <>
      <PageHeader
        title={title}
        description={readOnly ? 'Visualização — seu perfil não permite editar este veículo.' : 'Campos com * são obrigatórios.'}
        breadcrumbs={vehicle
          ? [{ label: 'Veículos', to: '/veiculos' }, { label: formatPlate(vehicle.licensePlate), to: `/veiculos/${vehicle.id}` }, { label: 'Editar' }]
          : [{ label: 'Veículos', to: '/veiculos' }, { label: 'Novo' }]}
        action={vehicle && <AuditHistoryButton entity="Vehicle" id={vehicle.id} />}
      />
      <form onSubmit={handleSubmit} noValidate>
        <fieldset disabled={readOnly} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
          <Stack gap="md">
            <FormSection title="Identificação" description="Dados do documento do veículo (CRLV)." cols={3}>
              <MaskedInput label="Placa" withAsterisk mask={MASKS.plate} uppercase placeholder="ABC-1D23" description="Padrão antigo ou Mercosul" data-autofocus {...form.getInputProps('licensePlate')} />
              <MaskedInput label="RENAVAM" withAsterisk mask={MASKS.renavam} inputMode="numeric" {...form.getInputProps('renavam')} />
              <MaskedInput label="Chassi" withAsterisk mask={MASKS.chassis} uppercase {...form.getInputProps('chassis')} />
              <TextInput label="Fabricante" withAsterisk placeholder="Ex.: Scania" {...form.getInputProps('manufacturer')} />
              <TextInput label="Modelo" withAsterisk placeholder="Ex.: R 450" {...form.getInputProps('model')} />
              <TextInput label="Cor" {...form.getInputProps('color')} />
              <NumberInput label="Ano de fabricação" withAsterisk allowDecimal={false} allowNegative={false} thousandSeparator="" {...form.getInputProps('manufacturingYear')} />
              <NumberInput label="Ano do modelo" withAsterisk allowDecimal={false} allowNegative={false} thousandSeparator="" {...form.getInputProps('modelYear')} />
            </FormSection>

            <FormSection title="Características" cols={3}>
              <Select label="Tipo" withAsterisk data={toSelectData(VEHICLE_TYPE)} placeholder="Selecione" searchable {...form.getInputProps('type')} />
              <Select label="Categoria" data={toSelectData(VEHICLE_CATEGORY)} placeholder="Selecione" clearable allowDeselect description="Classe de peso" {...form.getInputProps('category')} />
              <Select label="Combustível" withAsterisk data={toSelectData(FUEL_TYPE)} {...form.getInputProps('fuelType')} />
              <NumberInput label="Capacidade de carga" suffix=" kg" thousandSeparator="." decimalSeparator="," decimalScale={2} allowNegative={false} {...form.getInputProps('cargoCapacityKg')} />
              <NumberInput label="Tara" suffix=" kg" thousandSeparator="." decimalSeparator="," decimalScale={2} allowNegative={false} {...form.getInputProps('tareWeightKg')} />
            </FormSection>

            <FormSection title="Controle" cols={3}>
              <NumberInput label={vehicle ? 'Hodômetro atual' : 'Hodômetro inicial'} withAsterisk={!vehicle} suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
                inputMode="numeric" disabled={!!vehicle}
                description={vehicle ? 'Atualize pela aba Quilometragem, que valida e guarda o histórico.' : 'Primeira leitura do histórico do veículo.'}
                {...form.getInputProps('currentOdometerKm')} />
              <NumberInput label={vehicle ? 'Horímetro atual' : 'Horímetro inicial'} suffix=" h" thousandSeparator="." decimalSeparator="," decimalScale={1} allowNegative={false}
                inputMode="numeric" disabled={!!vehicle}
                description={vehicle ? 'Atualize pela aba Manutenção, que valida e guarda o histórico.' : 'Primeira leitura do histórico, se o veículo tiver horímetro.'}
                {...form.getInputProps('hourMeter')} />
              <Select label="Situação" withAsterisk data={toSelectData(VEHICLE_STATUS)}
                description="Indisponível: fora de uso por outro motivo que não manutenção (documento, sinistro…)." {...form.getInputProps('status')} />
            </FormSection>

            <FormSection title="Aquisição">
              <DateInput label="Data de aquisição" valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" maxDate={new Date()} clearable {...form.getInputProps('acquisitionDate')} />
              <NumberInput label="Valor de aquisição" prefix="R$ " thousandSeparator="." decimalSeparator="," decimalScale={2} fixedDecimalScale allowNegative={false} {...form.getInputProps('acquisitionValue')} />
            </FormSection>

            <FormSection title="Observações" cols={1}>
              <Textarea aria-label="Observações" autosize minRows={3} maxLength={2000} placeholder="Informações adicionais sobre o veículo" {...form.getInputProps('notes')} />
            </FormSection>
          </Stack>
        </fieldset>
        {!readOnly && (
          <Box mt="md">
            <FormActions saving={save.isPending} onCancel={() => navigate(vehicle ? `/veiculos/${vehicle.id}` : '/veiculos')} submitLabel={vehicle ? 'Salvar alterações' : 'Cadastrar veículo'} />
          </Box>
        )}
      </form>
    </>
  );
}
