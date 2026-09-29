import { useState } from 'react';
import { Alert, Box, Button, Center, Paper, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertCircle, IconClockHour4 } from '@tabler/icons-react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { FullScreenLoader } from '../../auth/guards';
import { toApiError } from '../../api/errors';
import { required } from '../../lib/validators';

export function LoginPage() {
  const { status, login, sessionExpired } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const form = useForm({
    initialValues: { email: '', password: '' },
    validate: { email: required('E-mail'), password: required('Senha') },
  });

  if (status === 'loading') return <FullScreenLoader />;
  if (status === 'authenticated') return <Navigate to="/" replace />;

  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const handleSubmit = form.onSubmit(async ({ email, password }) => {
    setSubmitting(true);
    setError(null);
    try {
      await login(email.trim(), password);
      navigate(from, { replace: true });
    } catch (e) {
      setError(toApiError(e).message);
      form.setFieldValue('password', '');
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Center mih="100dvh" p="md" bg="var(--app-background)">
      <Box w="100%" maw={400}>
        <Stack align="center" gap={6} mb="lg">
          <img src="/favicon.svg" alt="" width={48} height={48} />
          <Title order={2} ta="center">
            Controle de Frota
          </Title>
          <Text c="dimmed" size="sm" ta="center">
            Entre com seu e-mail e senha para acessar.
          </Text>
        </Stack>

        <Paper p={{ base: 'lg', sm: 'xl' }} shadow="sm">
          <form onSubmit={handleSubmit} noValidate>
            <Stack gap="md">
              {sessionExpired && !error && (
                <Alert color="blue" icon={<IconClockHour4 size={18} />} variant="light">
                  Sua sessão expirou. Entre novamente para continuar de onde parou.
                </Alert>
              )}
              {error && (
                <Alert color="red" icon={<IconAlertCircle size={18} />} variant="light" role="alert">
                  {error}
                </Alert>
              )}
              <TextInput
                label="E-mail"
                type="email"
                autoComplete="username"
                inputMode="email"
                placeholder="nome@empresa.com.br"
                data-autofocus
                autoFocus
                {...form.getInputProps('email')}
              />
              <PasswordInput label="Senha" autoComplete="current-password" {...form.getInputProps('password')} />
              <Button type="submit" fullWidth loading={submitting} size="md">
                Entrar
              </Button>
              <Text size="xs" c="dimmed" ta="center">
                Esqueceu a senha? Peça ao administrador do sistema para redefini-la.
              </Text>
            </Stack>
          </form>
        </Paper>
      </Box>
    </Center>
  );
}
