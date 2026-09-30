import { Alert, Box, Select, Stack, Switch, TextInput, Textarea } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { IconAlertTriangle } from '@tabler/icons-react';
import dayjs from 'dayjs';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AddressFields, addressFromApi } from '../../components/AddressFields';
import { StatusBadge, toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { FormActions, FormSection, MASKS, MaskedInput, useUnsavedChangesGuard } from '../../components/forms';
import { PageHeader } from '../../components/PageHeader';
import { submitWithFeedback } from '../../components/useApiForm';
import { formatCpf, formatDate, formatPhone, formatZipCode, fromApiDate, toApiDate } from '../../lib/format';
import { combine, optional, required, rules } from '../../lib/validators';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { DRIVER_STATUS, LICENSE_CATEGORIES, LICENSE_STATE, driversApi, type Driver, type DriverStatus, type LicenseCategory } from './drivers';

const CATEGORY_OPTIONS = LICENSE_CATEGORIES.map((c) => ({ value: c, label: c }));

export function DriverFormPage() {
  const { id } = useParams();
  const detail = driversApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(driver) => <DriverForm driver={driver} />}</EntityFormPage>;
}

function DriverForm({ driver }: { driver?: Driver }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const save = driversApi.useSave(driver?.id);
  const readOnly = !can(driver ? PERMISSIONS.drivers.update : PERMISSIONS.drivers.create);
  const today = dayjs().startOf('day');

  const initialValues = {
    fullName: driver?.fullName ?? '',
    cpf: driver ? formatCpf(driver.cpf) : '',
    rg: driver?.rg ?? '',
    birthDate: fromApiDate(driver?.birthDate),
    phone: driver?.phone ? formatPhone(driver.phone) : '',
    email: driver?.email ?? '',
    address: { ...addressFromApi(driver?.address), zipCode: driver?.address.zipCode ? formatZipCode(driver.address.zipCode) : '' },
    licenseNumber: driver?.licenseNumber ?? '',
    licenseCategory: (driver?.licenseCategory ?? null) as LicenseCategory | null,
    licenseExpiresOn: fromApiDate(driver?.licenseExpiresOn),
    performsPaidActivity: driver?.performsPaidActivity ?? true,
    status: (driver?.status ?? 'Active') as DriverStatus,
    notes: driver?.notes ?? '',
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validateInputOnBlur: true,
    validate: {
      fullName: required('Nome completo'),
      cpf: combine(required('CPF'), rules.cpf),
      birthDate: (v) => {
        if (!v) return 'Data de nascimento: campo obrigatório.';
        const age = today.diff(dayjs(v), 'year');
        return age < 18 || age > 100 ? 'O motorista deve ter entre 18 e 100 anos.' : null;
      },
      phone: optional(rules.phone),
      email: optional(rules.email),
      address: { zipCode: optional(rules.zipCode) },
      licenseNumber: combine(required('Número da CNH'), rules.driverLicense),
      licenseCategory: (v) => (v ? null : 'Categoria da CNH: campo obrigatório.'),
      licenseExpiresOn: (v) => (v ? null : 'Validade da CNH: campo obrigatório.'),
    },
  });

  const guard = useUnsavedChangesGuard(form.isDirty());

  const handleSubmit = submitWithFeedback({
    form,
    mutation: save,
    successMessage: driver ? 'Motorista atualizado com sucesso.' : 'Motorista cadastrado com sucesso.',
    errorTitle: 'O motorista não foi salvo',
    onSuccess: (saved) => {
      guard.release();
      navigate(`/motoristas/${saved.id}`);
    },
    toBody: (v) => ({
      ...v,
      rg: v.rg || null,
      birthDate: toApiDate(v.birthDate),
      phone: v.phone || null,
      email: v.email || null,
      address: Object.fromEntries(Object.entries(v.address).map(([k, value]) => [k, value || null])) as Record<keyof typeof v.address, string | null>,
      licenseExpiresOn: toApiDate(v.licenseExpiresOn),
      notes: v.notes || null,
    }),
  });

  // Live feedback while typing the expiry date, not only after saving.
  const expiry = form.values.licenseExpiresOn ? dayjs(form.values.licenseExpiresOn) : null;
  const licenseWarning = expiry && (expiry.isBefore(today) ? 'Expired' : expiry.diff(today, 'day') <= 30 ? 'ExpiringSoon' : null);

  return (
    <>
      <PageHeader
        title={driver?.fullName ?? 'Novo motorista'}
        description={readOnly ? 'Visualização — seu perfil não permite editar este motorista.' : 'Campos com * são obrigatórios.'}
        breadcrumbs={driver
          ? [{ label: 'Motoristas', to: '/motoristas' }, { label: driver.fullName, to: `/motoristas/${driver.id}` }, { label: 'Editar' }]
          : [{ label: 'Motoristas', to: '/motoristas' }, { label: 'Novo' }]}
        action={driver && <AuditHistoryButton entity="Driver" id={driver.id} />}
      />
      <form onSubmit={handleSubmit} noValidate>
        <fieldset disabled={readOnly} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
          <Stack gap="md">
            <FormSection title="Dados pessoais" cols={3}>
              <TextInput label="Nome completo" withAsterisk autoComplete="name" data-autofocus {...form.getInputProps('fullName')} />
              <MaskedInput label="CPF" withAsterisk mask={MASKS.cpf} inputMode="numeric" placeholder="000.000.000-00" {...form.getInputProps('cpf')} />
              <TextInput label="RG" {...form.getInputProps('rg')} />
              <DateInput label="Data de nascimento" withAsterisk valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" maxDate={today.subtract(18, 'year').toDate()} defaultDate={today.subtract(30, 'year').toDate()} {...form.getInputProps('birthDate')} />
              <MaskedInput label="Telefone" mask={MASKS.phone} inputMode="tel" autoComplete="tel" placeholder="(00) 00000-0000" {...form.getInputProps('phone')} />
              <TextInput label="E-mail" type="email" inputMode="email" autoComplete="email" {...form.getInputProps('email')} />
            </FormSection>

            <FormSection title="Habilitação (CNH)" cols={3}>
              <MaskedInput label="Número de registro da CNH" withAsterisk mask={MASKS.driverLicense} inputMode="numeric" description="11 dígitos, impressos na CNH" {...form.getInputProps('licenseNumber')} />
              <Select label="Categoria" withAsterisk data={CATEGORY_OPTIONS} placeholder="Selecione" {...form.getInputProps('licenseCategory')} />
              <DateInput label="Validade" withAsterisk valueFormat="DD/MM/YYYY" placeholder="dd/mm/aaaa" {...form.getInputProps('licenseExpiresOn')} />
              <Switch
                label="Exerce atividade remunerada (EAR)"
                description="Marque se a CNH tem a observação EAR — exigida para quem dirige profissionalmente."
                mt="xs"
                {...form.getInputProps('performsPaidActivity', { type: 'checkbox' })}
              />
              {licenseWarning && (
                <Alert color={LICENSE_STATE[licenseWarning].color} icon={<IconAlertTriangle size={18} />} variant="light" style={{ gridColumn: '1 / -1' }}>
                  {licenseWarning === 'Expired'
                    ? `A CNH venceu em ${formatDate(toApiDate(form.values.licenseExpiresOn))}. O cadastro é permitido, mas o motorista não deve dirigir até renovar.`
                    : `A CNH vence em ${formatDate(toApiDate(form.values.licenseExpiresOn))}. Lembre o motorista de renovar.`}
                </Alert>
              )}
            </FormSection>

            <FormSection title="Endereço" description="Opcional." cols={3}>
              <AddressFields form={form} required={false} />
            </FormSection>

            <FormSection title="Situação e observações" cols={2}>
              <Select label="Situação" withAsterisk data={toSelectData(DRIVER_STATUS)} {...form.getInputProps('status')} />
              {driver && (
                <Box pt={28}>
                  <StatusBadge value={driver.licenseState} map={LICENSE_STATE} />
                </Box>
              )}
              <Textarea label="Observações" autosize minRows={3} maxLength={2000} style={{ gridColumn: '1 / -1' }} {...form.getInputProps('notes')} />
            </FormSection>
          </Stack>
        </fieldset>
        {!readOnly && (
          <Box mt="md">
            <FormActions saving={save.isPending} onCancel={() => navigate(driver ? `/motoristas/${driver.id}` : '/motoristas')} submitLabel={driver ? 'Salvar alterações' : 'Cadastrar motorista'} />
          </Box>
        )}
      </form>
    </>
  );
}
