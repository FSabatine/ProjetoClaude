import type { ReactNode } from 'react';
import { Button, Center, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import { IconAlertTriangle, IconFilterOff, IconRefresh } from '@tabler/icons-react';
import { toApiError } from '../api/errors';

interface EmptyStateProps {
  icon: ReactNode;
  title: string;
  description: string;
  action?: ReactNode;
}

/** "No data yet" — explains the screen and offers the first action. */
export function EmptyState({ icon, title, description, action }: EmptyStateProps) {
  return (
    <Center py={48} px="md">
      <Stack align="center" gap="sm" maw={420} ta="center">
        <ThemeIcon size={56} radius="xl" variant="light" color="gray">
          {icon}
        </ThemeIcon>
        <Title order={4}>{title}</Title>
        <Text c="dimmed" size="sm">
          {description}
        </Text>
        {action && <div style={{ marginTop: 8 }}>{action}</div>}
      </Stack>
    </Center>
  );
}

export function NoResultsState({ onClear }: { onClear: () => void }) {
  return (
    <EmptyState
      icon={<IconFilterOff size={28} />}
      title="Nenhum resultado"
      description="Nenhum registro corresponde à busca ou aos filtros aplicados."
      action={
        <Button variant="default" onClick={onClear}>
          Limpar filtros
        </Button>
      }
    />
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { message } = toApiError(error);
  return (
    <EmptyState
      icon={<IconAlertTriangle size={28} />}
      title="Não foi possível carregar os dados"
      description={message}
      action={
        onRetry && (
          <Button variant="default" leftSection={<IconRefresh size={16} />} onClick={onRetry}>
            Tentar novamente
          </Button>
        )
      }
    />
  );
}
