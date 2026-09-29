import { Select, TextInput } from '@mantine/core';
import type { UseFormReturnType } from '@mantine/form';
import { BRAZILIAN_STATES } from '../lib/validators';
import { MASKS, MaskedInput } from './forms';

export interface AddressValues {
  street: string;
  number: string;
  complement: string;
  neighborhood: string;
  city: string;
  state: string | null;
  zipCode: string;
}

export const emptyAddress = (): AddressValues => ({
  street: '', number: '', complement: '', neighborhood: '', city: '', state: null, zipCode: '',
});

export const addressFromApi = (a: Partial<Record<keyof AddressValues, string | null>> | undefined): AddressValues => ({
  street: a?.street ?? '',
  number: a?.number ?? '',
  complement: a?.complement ?? '',
  neighborhood: a?.neighborhood ?? '',
  city: a?.city ?? '',
  state: a?.state ?? null,
  zipCode: a?.zipCode ?? '',
});

const STATE_OPTIONS = BRAZILIAN_STATES.map((uf) => ({ value: uf, label: uf }));

// eslint-disable-next-line @typescript-eslint/no-explicit-any -- works with any form that has an `address` group
export function AddressFields({ form, required }: { form: UseFormReturnType<any>; required: boolean }) {
  return (
    <>
      <MaskedInput
        label="CEP"
        withAsterisk={required}
        mask={MASKS.zipCode}
        inputMode="numeric"
        autoComplete="postal-code"
        placeholder="00000-000"
        {...form.getInputProps('address.zipCode')}
      />
      <TextInput label="Logradouro" withAsterisk={required} autoComplete="address-line1" {...form.getInputProps('address.street')} />
      <TextInput label="Número" withAsterisk={required} {...form.getInputProps('address.number')} />
      <TextInput label="Complemento" autoComplete="address-line2" {...form.getInputProps('address.complement')} />
      <TextInput label="Bairro" withAsterisk={required} {...form.getInputProps('address.neighborhood')} />
      <TextInput label="Cidade" withAsterisk={required} autoComplete="address-level2" {...form.getInputProps('address.city')} />
      <Select
        label="UF"
        withAsterisk={required}
        data={STATE_OPTIONS}
        searchable
        clearable={!required}
        allowDeselect={!required}
        placeholder="Selecione"
        {...form.getInputProps('address.state')}
      />
    </>
  );
}
