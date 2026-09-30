import { Box, NumberInput, Select, Stack, TextInput, Textarea, Title } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { FormActions, FormSection, MASKS, MaskedInput, useUnsavedChangesGuard } from '../../components/forms';
import { PageHeader } from '../../components/PageHeader';
import { submitWithFeedback } from '../../components/useApiForm';
import { formatPlate } from '../../lib/format';
import { combine, required, rules } from '../../lib/validators';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { DocumentsPanel } from '../documents/DocumentsPanel';
import {
  CAPACITY_UNIT,
  IMPLEMENT_STATUS,
  IMPLEMENT_TYPE,
  implementsApi,
  type CapacityUnit,
  type Implement,
  type ImplementStatus,
  type ImplementType,
} from './implements';

const CURRENT_YEAR = new Date().getFullYear();
type NumberValue = number | string;
const toNumber = (v: NumberValue) => (v === '' || v === null ? null : Number(v));
const UNIT_OPTIONS = (Object.keys(CAPACITY_UNIT) as CapacityUnit[]).map((value) => ({ value, label: CAPACITY_UNIT[value].long }));

/** Sensible unit for each implement type (helpful default — UX_UI.md). */
const DEFAULT_UNIT: Partial<Record<ImplementType, CapacityUnit>> = { Tanker: 'Liters', BoxBody: 'CubicMeters', Sider: 'Kg', SemiTrailer: 'Kg', Trailer: 'Kg' };

export function ImplementFormPage() {
  const { id } = useParams();
  const detail = implementsApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(implement) => <ImplementForm implement={implement} />}</EntityFormPage>;
}

function ImplementForm({ implement }: { implement?: Implement }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const save = implementsApi.useSave(implement?.id);
  const readOnly = !can(implement ? PERMISSIONS.implements.update : PERMISSIONS.implements.create);

  const initialValues = {
    licensePlate: implement ? formatPlate(implement.licensePlate) : '',
    renavam: implement?.renavam ?? '',
    chassis: implement?.chassis ?? '',
    manufacturer: implement?.manufacturer ?? '',
    model: implement?.model ?? '',
    manufacturingYear: (implement?.manufacturingYear ?? CURRENT_YEAR) as NumberValue,
    modelYear: (implement?.modelYear ?? CURRENT_YEAR) as NumberValue,
    type: (implement?.type ?? null) as ImplementType | null,
    capacity: (implement?.capacity ?? '') as NumberValue,
    capacityUnit: (implement?.capacityUnit ?? null) as CapacityUnit | null,
    tareWeightKg: (implement?.tareWeightKg ?? '') as NumberValue,
    status: (implement?.status ?? 'Available') as ImplementStatus,
    notes: implement?.notes ?? '',
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
      manufacturingYear: (v) => (v === '' ? 'Ano de fabricação: campo obrigatório.' : null),
      modelYear: (v, values) =>
        v === '' ? 'Ano do modelo: campo obrigatório.'
          : Number(v) !== Number(values.manufacturingYear) && Number(v) !== Number(values.manufacturingYear) + 1
            ? 'Deve ser igual ao ano de fabricação ou o ano seguinte.' : null,
      type: (v) => (v ? null : 'Tipo: campo obrigatório.'),
      capacityUnit: (v, values) => (values.capacity !== '' && !v ? 'Informe a unidade da capacidade.' : null),
    },
    onValuesChange: (values, previous) => {
      if (values.type !== previous.type && values.type && !values.capacityUnit && DEFAULT_UNIT[values.type])
        form.setFieldValue('capacityUnit', DEFAULT_UNIT[values.type]!);
    },
  });

  const guard = useUnsavedChangesGuard(form.isDirty());

  const handleSubmit = submitWithFeedback({
    form,
    mutation: save,
    successMessage: implement ? 'Implemento atualizado com sucesso.' : 'Implemento cadastrado com sucesso.',
    errorTitle: 'O implemento não foi salvo',
    onSuccess: () => {
      guard.release();
      navigate('/implementos');
    },
    toBody: (v) => ({
      ...v,
      manufacturingYear: toNumber(v.manufacturingYear),
      modelYear: toNumber(v.modelYear),
      capacity: toNumber(v.capacity),
      capacityUnit: v.capacity === '' ? null : v.capacityUnit,
      tareWeightKg: toNumber(v.tareWeightKg),
      notes: v.notes || null,
    }),
  });

  return (
    <>
      <PageHeader
        title={implement ? `${formatPlate(implement.licensePlate)} · ${IMPLEMENT_TYPE[implement.type].label}` : 'Novo implemento'}
        description={readOnly ? 'Visualização — seu perfil não permite editar este implemento.' : 'Campos com * são obrigatórios.'}
        breadcrumbs={[{ label: 'Implementos', to: '/implementos' }, { label: implement ? 'Editar' : 'Novo' }]}
        action={implement && <AuditHistoryButton entity="Implement" id={implement.id} />}
      />
      <form onSubmit={handleSubmit} noValidate>
        <fieldset disabled={readOnly} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
          <Stack gap="md">
            <FormSection title="Identificação" description="Dados do documento do implemento (CRLV)." cols={3}>
              <MaskedInput label="Placa" withAsterisk mask={MASKS.plate} uppercase placeholder="ABC-1D23" data-autofocus {...form.getInputProps('licensePlate')} />
              <MaskedInput label="RENAVAM" withAsterisk mask={MASKS.renavam} inputMode="numeric" {...form.getInputProps('renavam')} />
              <MaskedInput label="Chassi" withAsterisk mask={MASKS.chassis} uppercase {...form.getInputProps('chassis')} />
              <TextInput label="Fabricante" withAsterisk placeholder="Ex.: Randon" {...form.getInputProps('manufacturer')} />
              <TextInput label="Modelo" withAsterisk {...form.getInputProps('model')} />
              <Select label="Tipo" withAsterisk data={toSelectData(IMPLEMENT_TYPE)} placeholder="Selecione" {...form.getInputProps('type')} />
              <NumberInput label="Ano de fabricação" withAsterisk allowDecimal={false} allowNegative={false} thousandSeparator="" {...form.getInputProps('manufacturingYear')} />
              <NumberInput label="Ano do modelo" withAsterisk allowDecimal={false} allowNegative={false} thousandSeparator="" {...form.getInputProps('modelYear')} />
            </FormSection>

            <FormSection title="Capacidade e controle" cols={3}>
              <NumberInput label="Capacidade" thousandSeparator="." decimalSeparator="," decimalScale={2} allowNegative={false} {...form.getInputProps('capacity')} />
              <Select label="Unidade da capacidade" data={UNIT_OPTIONS} placeholder="Selecione" clearable allowDeselect {...form.getInputProps('capacityUnit')} />
              <NumberInput label="Tara" suffix=" kg" thousandSeparator="." decimalSeparator="," decimalScale={2} allowNegative={false} {...form.getInputProps('tareWeightKg')} />
              <Select label="Situação" withAsterisk data={toSelectData(IMPLEMENT_STATUS)} {...form.getInputProps('status')} />
            </FormSection>

            <FormSection title="Observações" cols={1}>
              <Textarea aria-label="Observações" autosize minRows={3} maxLength={2000} {...form.getInputProps('notes')} />
            </FormSection>
          </Stack>
        </fieldset>
        {!readOnly && (
          <Box mt="md">
            <FormActions saving={save.isPending} onCancel={() => navigate('/implementos')} submitLabel={implement ? 'Salvar alterações' : 'Cadastrar implemento'} />
          </Box>
        )}
      </form>
      {implement && can(PERMISSIONS.documents.view) && (
        <Box mt="xl">
          <Title order={3} fz="lg" mb="sm">Documentos do implemento</Title>
          <DocumentsPanel ownerType="Implement" ownerId={implement.id} />
        </Box>
      )}
    </>
  );
}
