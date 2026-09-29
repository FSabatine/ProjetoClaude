import { Box, Stack, Switch, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AddressFields, addressFromApi } from '../../components/AddressFields';
import { EntityFormPage } from '../../components/EntityFormPage';
import { FormActions, FormSection, MASKS, MaskedInput, useUnsavedChangesGuard } from '../../components/forms';
import { PageHeader } from '../../components/PageHeader';
import { submitWithFeedback } from '../../components/useApiForm';
import { formatCnpj, formatPhone, formatZipCode } from '../../lib/format';
import { combine, optional, required, rules } from '../../lib/validators';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { companiesApi, useCurrentCompany, type Company } from './companies';

/** Platform admin: create/edit any company. */
export function CompanyFormPage() {
  const { id } = useParams();
  const detail = companiesApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(company) => <CompanyForm company={company} backTo="/empresas" />}</EntityFormPage>;
}

/** Any user with companies.view: their own company (editable with companies.update). */
export function MyCompanyPage() {
  const current = useCurrentCompany();
  return <EntityFormPage id="current" detail={current}>{(company) => <CompanyForm company={company} backTo="/" isOwn />}</EntityFormPage>;
}

function CompanyForm({ company, backTo, isOwn = false }: { company?: Company; backTo: string; isOwn?: boolean }) {
  const navigate = useNavigate();
  const { can, user } = useAuth();
  const save = companiesApi.useSave(company?.id);
  const isOwnCompany = isOwn || company?.id === user?.companyId;
  const readOnly = !(can(PERMISSIONS.companies.manage) || (isOwnCompany && can(PERMISSIONS.companies.update)));

  const initialValues = {
    legalName: company?.legalName ?? '',
    tradeName: company?.tradeName ?? '',
    cnpj: company ? formatCnpj(company.cnpj) : '',
    stateRegistration: company?.stateRegistration ?? '',
    email: company?.email ?? '',
    phone: company?.phone ? formatPhone(company.phone) : '',
    address: { ...addressFromApi(company?.address), zipCode: company?.address.zipCode ? formatZipCode(company.address.zipCode) : '' },
    isActive: company?.isActive ?? true,
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validateInputOnBlur: true,
    validate: {
      legalName: required('Razão social'),
      cnpj: combine(required('CNPJ'), rules.cnpj),
      email: optional(rules.email),
      phone: optional(rules.phone),
      address: {
        zipCode: combine(required('CEP'), rules.zipCode),
        street: required('Logradouro'),
        number: required('Número'),
        neighborhood: required('Bairro'),
        city: required('Cidade'),
        state: (v: string | null) => (v ? null : 'UF: campo obrigatório.'),
      },
    },
  });

  const guard = useUnsavedChangesGuard(form.isDirty());

  const handleSubmit = submitWithFeedback({
    form,
    mutation: save,
    successMessage: company ? 'Empresa atualizada com sucesso.' : 'Empresa cadastrada. Crie agora o primeiro usuário administrador dela.',
    errorTitle: 'A empresa não foi salva',
    onSuccess: (saved) => {
      guard.release();
      navigate(company ? backTo : `/usuarios/novo?companyId=${saved.id}`);
    },
    toBody: (v) => ({
      ...v,
      tradeName: v.tradeName || null,
      stateRegistration: v.stateRegistration || null,
      email: v.email || null,
      phone: v.phone || null,
      address: Object.fromEntries(Object.entries(v.address).map(([k, value]) => [k, value || null])) as Record<keyof typeof v.address, string | null>,
    }),
  });

  return (
    <>
      <PageHeader
        title={company ? (company.tradeName ?? company.legalName) : 'Nova empresa'}
        description={readOnly ? 'Visualização — seu perfil não permite editar os dados da empresa.' : 'Campos com * são obrigatórios.'}
        breadcrumbs={isOwn ? undefined : [{ label: 'Empresas', to: '/empresas' }, { label: company ? 'Editar' : 'Nova' }]}
        action={company && <AuditHistoryButton entity="Company" id={company.id} />}
      />
      <form onSubmit={handleSubmit} noValidate>
        <fieldset disabled={readOnly} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
          <Stack gap="md">
            <FormSection title="Dados cadastrais">
              <TextInput label="Razão social" withAsterisk data-autofocus {...form.getInputProps('legalName')} />
              <TextInput label="Nome fantasia" {...form.getInputProps('tradeName')} />
              <MaskedInput label="CNPJ" withAsterisk mask={MASKS.cnpj} uppercase placeholder="00.000.000/0000-00" description="Aceita o novo CNPJ alfanumérico" {...form.getInputProps('cnpj')} />
              <TextInput label="Inscrição estadual" description="Número ou ISENTO" {...form.getInputProps('stateRegistration')} />
              <TextInput label="E-mail" type="email" inputMode="email" autoComplete="email" {...form.getInputProps('email')} />
              <MaskedInput label="Telefone" mask={MASKS.phone} inputMode="tel" autoComplete="tel" {...form.getInputProps('phone')} />
            </FormSection>

            <FormSection title="Endereço" cols={3}>
              <AddressFields form={form} required />
            </FormSection>

            <FormSection title="Situação" cols={1}>
              <Switch
                label="Empresa ativa"
                description={isOwnCompany ? 'Você não pode inativar a sua própria empresa.' : 'Inativar bloqueia o acesso de todos os usuários desta empresa.'}
                disabled={isOwnCompany}
                {...form.getInputProps('isActive', { type: 'checkbox' })}
              />
            </FormSection>
          </Stack>
        </fieldset>
        {!readOnly && (
          <Box mt="md">
            <FormActions saving={save.isPending} onCancel={() => navigate(backTo)} submitLabel={company ? 'Salvar alterações' : 'Cadastrar empresa'} />
          </Box>
        )}
      </form>
    </>
  );
}
