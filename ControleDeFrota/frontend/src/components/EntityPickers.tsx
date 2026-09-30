import { useState } from 'react';
import { Select, type SelectProps } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import type { PagedResult } from '../api/crud';
import { formatPlate } from '../lib/format';

interface Option {
  value: string;
  label: string;
  description?: string;
  disabled?: boolean;
}

type PickerProps = Omit<SelectProps, 'data' | 'onChange' | 'value' | 'searchValue' | 'onSearchChange'> & {
  value: string | null;
  onChange: (value: string | null) => void;
  /** Label of the current value when it is not in the first page of results (e.g. editing). */
  initialLabel?: string | null;
};

/**
 * Server-side searchable select: fleets can have thousands of records, so options are fetched by the typed text
 * (debounced) instead of loading everything.
 */
function RemotePicker({ url, toOption, value, onChange, initialLabel, ...rest }: PickerProps & {
  url: string;
  toOption: (row: Record<string, unknown>) => Option;
}) {
  const [search, setSearch] = useState('');
  const [debounced] = useDebouncedValue(search, 300);
  const query = useQuery({
    queryKey: ['picker', url, debounced],
    queryFn: () => api.get<PagedResult<Record<string, unknown>>>(url, { params: { search: debounced || undefined, pageSize: 20 } }).then((r) => r.data),
  });
  const options = (query.data?.items ?? []).map(toOption);
  if (value && initialLabel && !options.some((o) => o.value === value)) options.unshift({ value, label: initialLabel });

  return (
    <Select
      searchable
      clearable
      allowDeselect
      nothingFoundMessage={query.isFetching ? 'Buscando…' : 'Nada encontrado'}
      {...rest}
      data={options.map(({ value: v, label, disabled }) => ({ value: v, label, disabled }))}
      value={value}
      onChange={onChange}
      searchValue={search}
      onSearchChange={setSearch}
      filter={({ options: all }) => all}
    />
  );
}

export function VehiclePicker(props: PickerProps) {
  return (
    <RemotePicker
      url="/vehicles"
      placeholder="Busque pela placa ou modelo"
      {...props}
      toOption={(v) => ({
        value: String(v.id),
        label: `${formatPlate(String(v.licensePlate))} · ${v.manufacturer} ${v.model}`,
        disabled: v.status === 'Inactive',
      })}
    />
  );
}

export function DriverPicker({ excludeInactive = true, ...props }: PickerProps & { excludeInactive?: boolean }) {
  return (
    <RemotePicker
      url="/drivers"
      placeholder="Busque pelo nome ou CPF"
      {...props}
      toOption={(d) => ({
        value: String(d.id),
        label: [
          String(d.fullName),
          d.currentVehiclePlate ? `com ${formatPlate(String(d.currentVehiclePlate))}` : null,
          excludeInactive && d.status === 'OnLeave' ? 'afastado' : null,
          excludeInactive && d.licenseState === 'Expired' ? 'CNH vencida' : null,
        ].filter(Boolean).join(' · '),
        // Same rules the API enforces for new assignments (ADR-020) — shown up front instead of failing on save.
        disabled: excludeInactive && (d.status !== 'Active' || d.licenseState === 'Expired'),
      })}
    />
  );
}
