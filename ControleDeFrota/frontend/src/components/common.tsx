import { useEffect, useState, type ReactNode } from 'react';
import { ActionIcon, Badge, Group, Menu, Paper, Select, TextInput, type ComboboxItem } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { IconDots, IconPencil, IconSearch, IconTrash, IconX } from '@tabler/icons-react';

export interface StatusOption {
  label: string;
  color: string;
}

/** Status is always text + color (never color alone — accessibility). */
export function StatusBadge<K extends string>({ value, map }: { value: K; map: Record<K, StatusOption> }) {
  const option = map[value];
  return <Badge color={option?.color ?? 'gray'}>{option?.label ?? value}</Badge>;
}

export const toSelectData = <K extends string>(map: Record<K, { label: string }>): ComboboxItem[] =>
  (Object.keys(map) as K[]).map((value) => ({ value, label: map[value].label }));

interface ToolbarFilter {
  key: string;
  placeholder: string;
  data: ComboboxItem[];
  value: string | null;
  onChange: (value: string | null) => void;
}

/** Search (debounced) + filters. Values live in the URL via useListParams. */
export function ListToolbar({ search, onSearch, searchPlaceholder, filters = [] }: {
  search: string;
  onSearch: (value: string) => void;
  searchPlaceholder: string;
  filters?: ToolbarFilter[];
}) {
  const [text, setText] = useState(search);
  const [debounced] = useDebouncedValue(text, 300);

  useEffect(() => setText(search), [search]);
  useEffect(() => {
    if (debounced !== search) onSearch(debounced);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only react to the debounced text
  }, [debounced]);

  return (
    <Paper p="sm" mb="md">
      <Group gap="sm" wrap="wrap">
        <TextInput
          aria-label={searchPlaceholder}
          placeholder={searchPlaceholder}
          leftSection={<IconSearch size={16} />}
          rightSection={
            text && (
              <ActionIcon variant="subtle" color="gray" aria-label="Limpar busca" onClick={() => setText('')}>
                <IconX size={14} />
              </ActionIcon>
            )
          }
          value={text}
          onChange={(e) => setText(e.currentTarget.value)}
          style={{ flex: '1 1 260px' }}
        />
        {filters.map((f) => (
          <Select
            key={f.key}
            aria-label={f.placeholder}
            placeholder={f.placeholder}
            data={f.data}
            value={f.value}
            onChange={f.onChange}
            clearable
            allowDeselect
            style={{ flex: '0 1 200px', minWidth: 160 }}
          />
        ))}
      </Group>
    </Paper>
  );
}

/** Per-row "⋯" menu with the standard actions. */
export function RowActions({ onEdit, onDelete, extra }: { onEdit?: () => void; onDelete?: () => void; extra?: ReactNode }) {
  if (!onEdit && !onDelete && !extra) return null;
  return (
    <Menu position="bottom-end" withinPortal shadow="md">
      <Menu.Target>
        <ActionIcon variant="subtle" color="gray" aria-label="Ações do registro" size="lg">
          <IconDots size={18} />
        </ActionIcon>
      </Menu.Target>
      <Menu.Dropdown>
        {onEdit && (
          <Menu.Item leftSection={<IconPencil size={16} />} onClick={onEdit}>
            Editar
          </Menu.Item>
        )}
        {extra}
        {onDelete && (
          <Menu.Item color="red" leftSection={<IconTrash size={16} />} onClick={onDelete}>
            Excluir
          </Menu.Item>
        )}
      </Menu.Dropdown>
    </Menu>
  );
}
