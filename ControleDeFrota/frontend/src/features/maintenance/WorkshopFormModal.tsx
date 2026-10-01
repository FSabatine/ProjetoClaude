import { Modal, Select, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useEffect } from 'react';
import { AddressFields, addressFromApi } from '../../components/AddressFields';
import { FormActions } from '../../components/forms';
import { submitWithFeedback } from '../../components/useApiForm';
import { formatCnpj, formatCpf, formatPhone, formatZipCode } from '../../lib/format';
import { isValidCnpj, isValidCpf, normalizeDocument, optional, required, rules } from '../../lib/validators';
import { WORKSHOP_STATUS, workshopsApi, type Workshop, type WorkshopStatus } from './maintenance';

const formatDocument = (value: string) => (normalizeDocument(value).length > 11 ? formatCnpj(value) : formatCpf(value));

export function WorkshopFormModal({ opened, onClose, workshop }: { opened: boolean; onClose: () => void; workshop?: Workshop }) {
  const save = workshopsApi.useSave(workshop?.id);

  const initialValues = {
    name: workshop?.name ?? '',
    document: workshop?.document ? formatDocument(workshop.document) : '',
    phone: workshop?.phone ? formatPhone(workshop.phone) : '',
    email: workshop?.email ?? '',
    address: { ...addressFromApi(workshop?.address), zipCode: workshop?.address.zipCode ? formatZipCode(workshop.address.zipCode) : '' },
    specialties: workshop?.specialties ?? '',
    status: (workshop?.status ?? 'Active') as WorkshopStatus,
    notes: workshop?.notes ?? '',
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validateInputOnBlur: true,
    validate: {
      name: required('Nome'),
      document: optional((v) => (isValidCpf(v) || isValidCnpj(v) ? null : 'Documento inválido. Informe um CPF ou CNPJ válido.')),
      phone: optional(rules.phone),
      email: optional(rules.email),
      address: { zipCode: optional(rules.zipCode) },
    },
  });

  useEffect(() => {
    if (opened) form.setValues(initialValues);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the modal (re)opens
  }, [opened, workshop]);

  const handleSubmit = submitWithFeedback({
    form,
    mutation: save,
    successMessage: workshop ? 'Oficina atualizada com sucesso.' : 'Oficina cadastrada com sucesso.',
    errorTitle: 'A oficina não foi salva',
    onSuccess: () => onClose(),
    toBody: (v) => ({
      ...v,
      document: v.document || null,
      phone: v.phone || null,
      email: v.email || null,
      address: Object.fromEntries(Object.entries(v.address).map(([k, value]) => [k, value || null])) as typeof v.address,
      specialties: v.specialties || null,
      notes: v.notes || null,
    }),
  });

  return (
    <Modal opened={opened} onClose={onClose} title={workshop ? 'Editar oficina' : 'Nova oficina'} centered size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack>
          <TextInput label="Nome" withAsterisk maxLength={150} data-autofocus {...form.getInputProps('name')} />
          <Stack gap="xs">
            <TextInput label="Documento (CPF ou CNPJ)" {...form.getInputProps('document')}
              onChange={(e) => form.setFieldValue('document', formatDocument(e.currentTarget.value))} />
            <TextInput label="Telefone" {...form.getInputProps('phone')} />
          </Stack>
          <TextInput label="E-mail" type="email" {...form.getInputProps('email')} />
          <AddressFields form={form} required={false} />
          <TextInput label="Especialidades" placeholder="Ex.: motor, elétrica, freios" maxLength={300} {...form.getInputProps('specialties')} />
          <Select label="Situação" withAsterisk data={Object.entries(WORKSHOP_STATUS).map(([value, { label }]) => ({ value, label }))} {...form.getInputProps('status')} />
          <Textarea label="Observações" autosize minRows={2} maxLength={2000} {...form.getInputProps('notes')} />
          <FormActions saving={save.isPending} onCancel={onClose} submitLabel={workshop ? 'Salvar alterações' : 'Cadastrar oficina'} />
        </Stack>
      </form>
    </Modal>
  );
}
