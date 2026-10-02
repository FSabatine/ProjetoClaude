import { useState } from 'react';
import { Select, type SelectProps } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import type { ListParams, PagedResult } from '../api/crud';
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
function RemotePicker({ url, toOption, value, onChange, initialLabel, params, ...rest }: PickerProps & {
  url: string;
  toOption: (row: Record<string, unknown>) => Option;
  /** Fixed filters sent with every search (e.g. only tires in stock of a size). */
  params?: ListParams;
}) {
  const [search, setSearch] = useState('');
  const [debounced] = useDebouncedValue(search, 300);
  const query = useQuery({
    queryKey: ['picker', url, debounced, params],
    queryFn: () => api.get<PagedResult<Record<string, unknown>>>(url, { params: { ...params, search: debounced || undefined, pageSize: 20 } }).then((r) => r.data),
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

export function ImplementPicker(props: PickerProps) {
  return (
    <RemotePicker
      url="/implements"
      placeholder="Busque pela placa ou modelo"
      {...props}
      toOption={(i) => ({
        value: String(i.id),
        label: `${formatPlate(String(i.licensePlate))} · ${i.manufacturer} ${i.model}`,
        disabled: i.status === 'Inactive',
      })}
    />
  );
}

/** Providers of repairs/retreads come from the workshop registry (Phase 3). */
export function WorkshopPicker(props: PickerProps) {
  return (
    <RemotePicker
      url="/workshops"
      placeholder="Busque a oficina ou recapadora"
      {...props}
      toOption={(w) => ({ value: String(w.id), label: String(w.name), disabled: w.status === 'Inactive' })}
    />
  );
}

/** Tires in stock by default; `size` keeps only the ones the position accepts. */
export function TirePicker({ status = 'InStock', tireSize, ...props }: PickerProps & { status?: string; tireSize?: string | null }) {
  return (
    <RemotePicker
      url="/tires"
      placeholder="Busque pelo número de fogo, marca ou modelo"
      params={{ status, size: tireSize ?? undefined }}
      {...props}
      toOption={(t) => ({
        value: String(t.id),
        label: [String(t.code), `${t.brand} ${t.modelName}`, String(t.size),
          t.currentTreadDepthMm !== null && t.currentTreadDepthMm !== undefined ? `${Number(t.currentTreadDepthMm).toLocaleString('pt-BR')} mm` : null,
        ].filter(Boolean).join(' · '),
      })}
    />
  );
}
