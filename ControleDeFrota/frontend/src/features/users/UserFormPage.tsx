import { useState } from 'react';
import { Alert, Box, Button, Checkbox, Group, Paper, PasswordInput, Select, SimpleGrid, Skeleton, Stack, Text, TextInput, Tooltip } from '@mantine/core';
import { useForm } from '@mantine/form';
import { modals } from '@mantine/modals';
import { IconInfoCircle, IconKey, IconLock } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../../api/client';
import type { PagedResult } from '../../api/crud';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { FormActions, FormSection, useUnsavedChangesGuard } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { submitWithFeedback } from '../../components/useApiForm';
import { formatDateTime } from '../../lib/format';
import { combine, required, rules } from '../../lib/validators';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { PASSWORD_HINT, passwordRule } from '../auth/ChangePasswordModal';
import { resetPassword, useRoles, USER_STATUS, usersApi, type User, type UserStatus } from './users';

export function UserFormPage() {
  const { id } = useParams();
  const detail = usersApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(user) => <UserForm user={user} />}</EntityFormPage>;
}

function UserForm({ user }: { user?: User }) {
  const navigate = useNavigate();
  const { can, user: me } = useAuth();
  const save = usersApi.useSave(user?.id);
  const roles = useRoles();
  const readOnly = !can(PERMISSIONS.users.manage);
  const isSelf = user?.id === me?.id;
  const isPlatformAdmin = can(PERMISSIONS.companies.manage);
  const [searchParams] = useSearchParams();

  const companies = useQuery({
    queryKey: ['companies', 'options'],
    queryFn: () => api.get<PagedResult<{ id: string; legalName: string; tradeName: string | null }>>('/companies', { params: { pageSize: 100 } }).then((r) => r.data.items),
    enabled: isPlatformAdmin && !user,
  });

  const initialValues = {
    name: user?.name ?? '',
    email: user?.email ?? '',
    password: '',
    // Arriving from "new company": preselect it so its first administrator is created there.
    companyId: (user?.companyId ?? searchParams.get('companyId') ?? me?.companyId ?? null) as string | null,
    status: (user?.status ?? 'Active') as UserStatus,
    roleIds: (user?.roles.map((r) => String(r.id)) ?? []) as string[],
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validateInputOnBlur: true,
    validate: {
      name: required('Nome'),
      email: combine(required('E-mail'), rules.email),
      password: (v) => (user ? null : passwordRule(v)),
      roleIds: (v) => (v.length ? null : 'Selecione pelo menos um papel de acesso.'),
    },
  });

  const guard = useUnsavedChangesGuard(form.isDirty());

  const handleSubmit = submitWithFeedback({
    form,
    mutation: save,
    successMessage: user ? 'Usuário atualizado com sucesso.' : 'Usuário criado. Informe a senha inicial a ele por um canal seguro.',
    errorTitle: 'O usuário não foi salvo',
    onSuccess: () => {
      guard.release();
      navigate('/usuarios');
    },
    toBody: (v) => ({
      name: v.name,
      email: v.email,
      status: v.status,
      roleIds: v.roleIds.map(Number),
      ...(user ? {} : { password: v.password, companyId: v.companyId }),
    }),
  });

  return (
    <>
      <PageHeader
        title={user?.name ?? 'Novo usuário'}
        description={user ? user.email : 'O e-mail será o login de acesso.'}
        breadcrumbs={[{ label: 'Usuários', to: '/usuarios' }, { label: user ? 'Editar' : 'Novo' }]}
        action={
          user && (
            <Group gap="xs">
              {!readOnly && !isSelf && <ResetPasswordButton user={user} />}
              <AuditHistoryButton entity="User" id={user.id} />
            </Group>
          )
        }
      />
      {isSelf && (
        <Alert icon={<IconInfoCircle size={18} />} mb="md" variant="light">
          Este é o seu usuário. Por segurança, você não pode alterar os próprios papéis nem se inativar — peça a outro administrador.
          Para trocar a sua senha, use o menu no canto superior direito.
        </Alert>
      )}
      {user?.lockoutEndAt && new Date(user.lockoutEndAt) > new Date() && (
        <Alert color="red" icon={<IconLock size={18} />} mb="md" variant="light">
          Acesso bloqueado até {formatDateTime(user.lockoutEndAt)} por excesso de tentativas de login. Redefinir a senha desbloqueia imediatamente.
        </Alert>
      )}
      <form onSubmit={handleSubmit} noValidate>
        <fieldset disabled={readOnly} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
          <Stack gap="md">
            <FormSection title="Dados de acesso">
              <TextInput label="Nome" withAsterisk autoComplete="off" data-autofocus {...form.getInputProps('name')} />
              <TextInput label="E-mail (login)" withAsterisk type="email" inputMode="email" autoComplete="off" {...form.getInputProps('email')} />
              {!user && (
                <PasswordInput label="Senha inicial" withAsterisk autoComplete="new-password" description={PASSWORD_HINT} {...form.getInputProps('password')} />
              )}
              {!user && isPlatformAdmin && (
                <Select
                  label="Empresa"
                  withAsterisk
                  searchable
                  data={(companies.data ?? []).map((c) => ({ value: c.id, label: c.tradeName ?? c.legalName }))}
                  description="Somente administradores da plataforma criam usuários em outras empresas."
                  {...form.getInputProps('companyId')}
                />
              )}
              <Select label="Situação" withAsterisk data={toSelectData(USER_STATUS)} disabled={isSelf} description={isSelf ? 'Você não pode inativar o próprio usuário.' : 'Inativar encerra as sessões abertas.'} {...form.getInputProps('status')} />
            </FormSection>

            <Paper p={{ base: 'md', sm: 'lg' }}>
              <Text fw={600} mb={2}>Papéis de acesso <Text span c="red">*</Text></Text>
              <Text size="sm" c="dimmed" mb="md">
                As permissões do usuário são a soma das permissões dos papéis marcados. Veja o detalhe em “Papéis e permissões”.
              </Text>
              {roles.isLoading ? (
                <SimpleGrid cols={{ base: 1, sm: 2 }}>{[1, 2, 3, 4].map((i) => <Skeleton key={i} height={56} />)}</SimpleGrid>
              ) : (
                <Checkbox.Group {...form.getInputProps('roleIds')}>
                  <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="sm">
                    {roles.data?.map((role) => {
                      const blocked = !role.isAssignable;
                      return (
                        <Tooltip key={role.id} label="Este papel concede permissões que você não possui." disabled={!blocked} position="top-start">
                          <Checkbox.Card value={String(role.id)} disabled={blocked || isSelf} p="sm" radius="md" style={{ opacity: blocked ? 0.55 : 1 }}>
                            <Group wrap="nowrap" align="flex-start" gap="sm">
                              <Checkbox.Indicator mt={2} />
                              <div>
                                <Text size="sm" fw={600}>{role.name}</Text>
                                <Text size="xs" c="dimmed">{role.description}</Text>
                              </div>
                            </Group>
                          </Checkbox.Card>
                        </Tooltip>
                      );
                    })}
                  </SimpleGrid>
                </Checkbox.Group>
              )}
              {form.errors.roleIds && <Text c="red" size="xs" mt="xs">{form.errors.roleIds}</Text>}
            </Paper>
          </Stack>
        </fieldset>
        {!readOnly && (
          <Box mt="md">
            <FormActions saving={save.isPending} onCancel={() => navigate('/usuarios')} submitLabel={user ? 'Salvar alterações' : 'Criar usuário'} />
          </Box>
        )}
      </form>
    </>
  );
}

function ResetPasswordButton({ user }: { user: User }) {
  const open = () => modals.open({ title: `Redefinir senha de ${user.name}`, children: <ResetPasswordForm user={user} /> });
  return (
    <Button variant="default" leftSection={<IconKey size={18} />} onClick={open}>
      Redefinir senha
    </Button>
  );
}

function ResetPasswordForm({ user }: { user: User }) {
  const [saving, setSaving] = useState(false);
  const form = useForm({ initialValues: { newPassword: '' }, validate: { newPassword: passwordRule } });
  const submit = form.onSubmit(async ({ newPassword }) => {
    setSaving(true);
    try {
      await resetPassword(user.id, newPassword);
      notifySuccess('Senha redefinida. As sessões do usuário foram encerradas e o bloqueio, se havia, foi removido.');
      modals.closeAll();
    } catch (e) {
      form.setErrors(notifyError(e, 'A senha não foi redefinida').fieldErrors);
    } finally {
      setSaving(false);
    }
  });
  return (
    <form onSubmit={submit} noValidate>
      <Stack gap="sm">
        <PasswordInput label="Nova senha" withAsterisk description={PASSWORD_HINT} autoComplete="new-password" data-autofocus {...form.getInputProps('newPassword')} />
        <Text size="xs" c="dimmed">Informe a nova senha ao usuário por um canal seguro. Todas as sessões dele serão encerradas.</Text>
        <Group justify="flex-end">
          <Button variant="default" onClick={() => modals.closeAll()} disabled={saving}>Cancelar</Button>
          <Button type="submit" loading={saving}>Redefinir senha</Button>
        </Group>
      </Stack>
    </form>
  );
}
