import { useState } from 'react';
import { Button, Group, PasswordInput, Stack, Text } from '@mantine/core';
import { useForm } from '@mantine/form';
import { modals } from '@mantine/modals';
import { api } from '../../api/client';
import { notifyError, notifySuccess } from '../../components/notify';
import { required } from '../../lib/validators';

export const PASSWORD_HINT = 'Mínimo de 10 caracteres, com letras e números. Não use seu e-mail.';

export const passwordRule = (value: string) => {
  if (value.length < 10) return 'A senha deve ter pelo menos 10 caracteres.';
  if (!/[A-Za-zÀ-ÿ]/.test(value) || !/\d/.test(value)) return 'A senha deve conter letras e números.';
  return null;
};

function ChangePasswordForm() {
  const [saving, setSaving] = useState(false);
  const form = useForm<{ currentPassword: string; newPassword: string; confirmation: string }>({
    initialValues: { currentPassword: '', newPassword: '', confirmation: '' },
    validate: {
      currentPassword: required('Senha atual'),
      newPassword: passwordRule,
      confirmation: (value, values) => (value === values.newPassword ? null : 'As senhas não conferem.'),
    },
  });

  const submit = form.onSubmit(async ({ currentPassword, newPassword }) => {
    setSaving(true);
    try {
      await api.post('/auth/change-password', { currentPassword, newPassword });
      notifySuccess('Senha alterada. As sessões abertas em outros dispositivos foram encerradas.');
      modals.closeAll();
    } catch (e) {
      const apiError = notifyError(e, 'A senha não foi alterada');
      form.setErrors(apiError.fieldErrors);
    } finally {
      setSaving(false);
    }
  });

  return (
    <form onSubmit={submit} noValidate>
      <Stack gap="sm">
        <PasswordInput label="Senha atual" withAsterisk autoComplete="current-password" data-autofocus {...form.getInputProps('currentPassword')} />
        <PasswordInput
          label="Nova senha"
          withAsterisk
          autoComplete="new-password"
          description={PASSWORD_HINT}
          {...form.getInputProps('newPassword')}
        />
        <PasswordInput label="Confirme a nova senha" withAsterisk autoComplete="new-password" {...form.getInputProps('confirmation')} />
        <Text size="xs" c="dimmed">
          Por segurança, as sessões abertas em outros dispositivos serão encerradas.
        </Text>
        <Group justify="flex-end" mt="xs">
          <Button variant="default" onClick={() => modals.closeAll()} disabled={saving}>
            Cancelar
          </Button>
          <Button type="submit" loading={saving}>
            Alterar senha
          </Button>
        </Group>
      </Stack>
    </form>
  );
}

export const openChangePassword = () => modals.open({ title: 'Trocar senha', children: <ChangePasswordForm /> });
