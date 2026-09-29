import { useEffect, useRef, type ReactNode } from 'react';
import { Button, Group, InputBase, Paper, SimpleGrid, Stack, Text, Title, type InputBaseProps } from '@mantine/core';
import { modals } from '@mantine/modals';
import { IMaskInput } from 'react-imask';
import { useBlocker } from 'react-router-dom';

type MaskedInputProps = InputBaseProps & {
  mask: string | { mask: string }[];
  value?: string;
  onChange?: (value: string) => void;
  onBlur?: () => void;
  name?: string;
  placeholder?: string;
  autoComplete?: string;
  inputMode?: 'numeric' | 'text' | 'tel' | 'email';
  /** Uppercase letters as the user types (plates, alphanumeric CNPJ, chassis). */
  uppercase?: boolean;
};

/**
 * TextInput with a mask. The form keeps the masked text; the backend normalizes (strips separators),
 * so what the user sees is exactly what they typed.
 */
export function MaskedInput({ mask, value, onChange, onBlur, uppercase, inputMode = 'text', ...rest }: MaskedInputProps) {
  // IMask options are forwarded to IMaskInput; Mantine's polymorphic typings don't know them.
  const maskOptions: object = {
    mask,
    definitions: { A: /[A-Za-z]/, X: /[A-Za-z0-9]/, '0': /[0-9]/ },
    prepareChar: uppercase ? (c: string) => c.toUpperCase() : undefined,
    onAccept: (v: string) => onChange?.(v),
  };
  return <InputBase {...rest} component={IMaskInput} {...maskOptions} value={value ?? ''} inputMode={inputMode} onBlur={onBlur} />;
}

export const MASKS = {
  cpf: '000.000.000-00',
  /** Alphanumeric CNPJ: 12 letters/digits + 2 check digits. */
  cnpj: 'XX.XXX.XXX/XXXX-00',
  zipCode: '00000-000',
  phone: [{ mask: '(00) 0000-0000' }, { mask: '(00) 00000-0000' }],
  /** Legacy ABC-1234 or Mercosul ABC-1D23 (the 5th character may be a letter). */
  plate: 'AAA-0X00',
  renavam: '00000000000',
  driverLicense: '00000000000',
  chassis: 'XXXXXXXXXXXXXXXXX',
};

/** Titled group of fields — logical grouping (UX_UI.md). */
export function FormSection({ title, description, children, cols = 2 }: { title: string; description?: string; children: ReactNode; cols?: number }) {
  return (
    <Paper p={{ base: 'md', sm: 'lg' }}>
      <Stack gap="md">
        <div>
          <Title order={4} fz="md">
            {title}
          </Title>
          {description && (
            <Text size="sm" c="dimmed" mt={2}>
              {description}
            </Text>
          )}
        </div>
        <SimpleGrid cols={{ base: 1, sm: Math.min(cols, 2), lg: cols }} spacing="md" verticalSpacing="sm">
          {children}
        </SimpleGrid>
      </Stack>
    </Paper>
  );
}

/** Sticky save bar: always reachable on long forms and on phones. */
export function FormActions({ saving, onCancel, submitLabel }: { saving: boolean; onCancel: () => void; submitLabel: string }) {
  return (
    <Paper
      p="sm"
      style={{ position: 'sticky', bottom: 'var(--mantine-spacing-md)', zIndex: 5 }}
      shadow="md"
    >
      <Group justify="flex-end" gap="sm">
        <Button variant="default" onClick={onCancel} disabled={saving}>
          Cancelar
        </Button>
        <Button type="submit" loading={saving} loaderProps={{ type: 'dots' }}>
          {saving ? 'Salvando…' : submitLabel}
        </Button>
      </Group>
    </Paper>
  );
}

/**
 * Asks before leaving a form with unsaved changes (router navigation and tab close).
 * Call release() right before navigating away after a successful save — the navigation happens
 * before React re-renders with the form no longer dirty.
 */
export function useUnsavedChangesGuard(isDirty: boolean) {
  const dirtyRef = useRef(isDirty);
  dirtyRef.current = isDirty;
  const blocker = useBlocker(({ currentLocation, nextLocation }) => dirtyRef.current && currentLocation.pathname !== nextLocation.pathname);

  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    modals.openConfirmModal({
      title: 'Sair sem salvar?',
      children: <Text size="sm">As alterações feitas neste formulário serão perdidas.</Text>,
      labels: { confirm: 'Sair sem salvar', cancel: 'Continuar editando' },
      confirmProps: { color: 'red' },
      onConfirm: () => blocker.proceed(),
      onCancel: () => blocker.reset(),
      onClose: () => blocker.state === 'blocked' && blocker.reset(),
    });
  }, [blocker]);

  useEffect(() => {
    if (!isDirty) return;
    const handler = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [isDirty]);

  return {
    release: () => {
      dirtyRef.current = false;
    },
  };
}

/** Destructive action confirmation that names the record. */
export function confirmDelete({ entity, name, onConfirm }: { entity: string; name: string; onConfirm: () => void }) {
  modals.openConfirmModal({
    title: `Excluir ${entity}?`,
    children: (
      <Text size="sm">
        O registro <b>{name}</b> deixará de aparecer nas listas e no painel. O histórico de alterações é mantido e a exclusão só pode
        ser revertida pelo suporte.
      </Text>
    ),
    labels: { confirm: `Excluir ${entity}`, cancel: 'Cancelar' },
    confirmProps: { color: 'red' },
    onConfirm,
  });
}
