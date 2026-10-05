import { useState } from 'react';
import { Button, Group, Modal, NumberInput, Select, Stack, Text, Textarea, TextInput } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { useNavigate, useParams } from 'react-router-dom';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { DriverPicker, VehiclePicker, WorkshopPicker } from '../../components/EntityPickers';
import { FormActions, FormSection, useUnsavedChangesGuard } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { submitWithFeedback } from '../../components/useApiForm';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import type { FileInfo } from '../operations/api';
import { expenseCategoriesApi, expensesApi } from './api';
import { PaymentStatusBadge } from './components';
import { PAYMENT_METHOD, type Expense } from './finance';

type NumberValue = number | string;
const toNumber = (v: NumberValue) => (v === '' || v === null ? null : Number(v));

export function ExpenseFormPage() {
  const { id } = useParams();
  const detail = expensesApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(e) => <ExpenseForm expense={e} />}</EntityFormPage>;
}

function ExpenseForm({ expense }: { expense?: Expense }) {
  const navigate = useNavigate();
  const isEdit = !!expense;
  const categories = expenseCategoriesApi.useList(false).data ?? [];
  const manualCategories = categories.filter((c) => !c.isSystemCategory);
  const create = expensesApi.useCreate();
  const update = expensesApi.useUpdate(expense?.id ?? '');
  const [files, setFiles] = useState<FileInfo[]>([]);
  const [paymentOpen, setPaymentOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);

  const initialValues = {
    expenseCategoryId: expense?.expenseCategoryId ?? null,
    costCenterId: expense?.costCenterId ?? null,
    vehicleId: expense?.vehicleId ?? null,
    driverId: expense?.driverId ?? null,
    workshopId: expense?.workshopId ?? null,
    supplierName: expense?.supplierName ?? '',
    description: expense?.description ?? '',
    referenceNumber: expense?.referenceNumber ?? '',
    expenseDate: expense ? new Date(expense.expenseDate) : new Date(),
    dueDate: expense?.dueDate ? new Date(expense.dueDate) : null,
    amount: (expense?.amount ?? '') as NumberValue,
    paymentMethod: expense?.paymentMethod ?? null,
    notes: expense?.notes ?? '',
  };
  const form = useForm<typeof initialValues>({
    initialValues,
    validateInputOnBlur: true,
    validate: {
      expenseCategoryId: (v) => (v ? null : 'Categoria: campo obrigatório.'),
      description: (v) => (v.trim() ? null : 'Descrição: campo obrigatório.'),
      expenseDate: (v) => (v ? null : 'Data da despesa: campo obrigatório.'),
      amount: (v) => (v === '' || Number(v) <= 0 ? 'O valor deve ser maior que zero.' : null),
      dueDate: (v, values) => (v && values.expenseDate && v < values.expenseDate ? 'Vencimento não pode ser anterior à data da despesa.' : null),
    },
  });
  const guard = useUnsavedChangesGuard(form.isDirty());

  const toBody = (v: typeof initialValues) => ({
    expenseCategoryId: v.expenseCategoryId,
    costCenterId: v.costCenterId,
    vehicleId: v.vehicleId,
    driverId: v.driverId,
    workshopId: v.workshopId,
    supplierName: v.supplierName.trim() || null,
    description: v.description.trim(),
    referenceNumber: v.referenceNumber.trim() || null,
    expenseDate: v.expenseDate ? formatIso(v.expenseDate) : null,
    dueDate: v.dueDate ? formatIso(v.dueDate) : null,
    amount: toNumber(v.amount),
    paymentMethod: v.paymentMethod,
    notes: v.notes.trim() || null,
  });

  const afterSave = (saved: Expense) => {
    guard.release();
    navigate(`/financeiro/despesas/${saved.id}/editar`);
  };

  const handleSubmit = isEdit
    ? submitWithFeedback({ form, mutation: update, successMessage: 'Despesa atualizada com sucesso.', errorTitle: 'A despesa não foi salva', onSuccess: afterSave, toBody })
    : submitWithFeedback({
      form, mutation: create, successMessage: 'Despesa registrada com sucesso.', errorTitle: 'A despesa não foi salva', onSuccess: afterSave,
      toBody: (v) => ({ ...toBody(v), fileIds: files.map((f) => f.id) }),
    });

  const saving = create.isPending || update.isPending;
  const title = isEdit ? 'Editar despesa' : 'Nova despesa';
  const categoryOptions = manualCategories.map((c) => ({ value: c.id, label: c.parentCategoryName ? `${c.parentCategoryName} / ${c.name}` : c.name }));
  const locked = isEdit && expense!.status === 'Cancelled';

  return (
    <>
      <PageHeader title={title}
        description={isEdit ? 'Combustível, manutenção e pneus não são editados aqui — vão pelo próprio módulo.' : 'Categoria, data e valor são obrigatórios.'}
        breadcrumbs={[{ label: 'Despesas', to: '/financeiro/despesas' }, { label: isEdit ? 'Editar' : 'Nova' }]}
        action={isEdit && <Group gap="xs">
          <PaymentStatusBadge value={expense!.status} />
          <AuditHistoryButton entity="Expense" id={expense!.id} />
        </Group>} />
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md" maw={880}>
          <FormSection title="Identificação" cols={2}>
            <Select label="Categoria" withAsterisk size="md" data={categoryOptions} searchable disabled={locked}
              description="Combustível, manutenção e pneus não aparecem: vêm automaticamente dos próprios módulos."
              {...form.getInputProps('expenseCategoryId')} />
            <TextInput label="Descrição" withAsterisk size="md" maxLength={200} disabled={locked} {...form.getInputProps('description')} />
            <VehiclePicker label="Veículo" size="md" disabled={locked} value={form.values.vehicleId} initialLabel={expense?.licensePlate}
              onChange={(v) => form.setFieldValue('vehicleId', v)} />
            <DriverPicker label="Motorista" size="md" excludeInactive={false} disabled={locked} value={form.values.driverId} initialLabel={expense?.driverName}
              onChange={(v) => form.setFieldValue('driverId', v)} />
            <WorkshopPicker label="Fornecedor (oficina cadastrada)" size="md" disabled={locked} value={form.values.workshopId} initialLabel={expense?.workshopName}
              onChange={(v) => form.setFieldValue('workshopId', v)} />
            <TextInput label="Nome do fornecedor (se não cadastrado)" size="md" maxLength={150} disabled={locked} {...form.getInputProps('supplierName')} />
            <TextInput label="Número de referência / nota fiscal" size="md" maxLength={60} disabled={locked} {...form.getInputProps('referenceNumber')} />
          </FormSection>

          <FormSection title="Valores" cols={2}>
            <DateInput label="Data da despesa" withAsterisk size="md" valueFormat="DD/MM/YYYY" disabled={locked} {...form.getInputProps('expenseDate')} />
            <DateInput label="Vencimento" size="md" valueFormat="DD/MM/YYYY" clearable disabled={locked}
              description="Deixe em branco se não houver vencimento." {...form.getInputProps('dueDate')} />
            <NumberInput label="Valor" withAsterisk size="md" prefix="R$ " decimalSeparator="," thousandSeparator="." decimalScale={2} allowNegative={false}
              disabled={locked} {...form.getInputProps('amount')} />
            <Select label="Forma de pagamento" size="md" data={toSelectData(PAYMENT_METHOD)} clearable disabled={locked} {...form.getInputProps('paymentMethod')} />
          </FormSection>

          {!isEdit && (
            <FormSection title="Anexos" description="Nota fiscal, recibo ou comprovante (PDF, JPG ou PNG)." cols={1}>
              <Group gap="sm">
                <UploadButton onUploaded={(f) => setFiles((x) => [...x, f])} />
              </Group>
              <AttachmentList files={files} onRemove={(f) => setFiles((x) => x.filter((y) => y.id !== f.id))} />
            </FormSection>
          )}

          <FormSection title="Observações" cols={1}>
            <Textarea label="Observações" autosize minRows={2} maxLength={1000} disabled={locked} {...form.getInputProps('notes')} />
          </FormSection>

          {isEdit && expense!.cancelledAt && (
            <Text size="sm" c="dimmed">Cancelada em {new Date(expense!.cancelledAt).toLocaleDateString('pt-BR')}. Motivo: {expense!.cancellationReason}</Text>
          )}

          {!locked && (
            <FormActions saving={saving} onCancel={() => navigate(-1)} submitLabel={isEdit ? 'Salvar alterações' : 'Registrar despesa'} />
          )}
          {isEdit && !locked && (
            <Group justify="flex-end" gap="sm">
              {expense!.canRegisterPayment && <Button variant="default" onClick={() => setPaymentOpen(true)}>Registrar pagamento</Button>}
              {expense!.canCancel && <Button variant="default" color="red" onClick={() => setCancelOpen(true)}>Cancelar despesa</Button>}
            </Group>
          )}
        </Stack>
      </form>
      {isEdit && <PaymentModal expense={expense!} opened={paymentOpen} onClose={() => setPaymentOpen(false)} />}
      {isEdit && <CancelModal expense={expense!} opened={cancelOpen} onClose={() => setCancelOpen(false)} />}
    </>
  );
}

function formatIso(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function PaymentModal({ expense, opened, onClose }: { expense: Expense; opened: boolean; onClose: () => void }) {
  const mutation = expensesApi.useRegisterPayment(expense.id);
  const form = useForm<{ paidAmount: NumberValue; paymentDate: Date | null }>({
    initialValues: { paidAmount: (expense.paidAmount ?? 0) as NumberValue, paymentDate: new Date() },
    validate: {
      paidAmount: (v) => (v === '' || Number(v) < 0 ? 'Valor pago não pode ser negativo.' : null),
      paymentDate: (v) => (v ? null : 'Data do pagamento: campo obrigatório.'),
    },
  });
  return (
    <Modal opened={opened} onClose={onClose} title="Registrar pagamento" centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate(
        { paidAmount: Number(v.paidAmount), paymentDate: formatIso(v.paymentDate!) },
        { onSuccess: () => { notifySuccess('Pagamento registrado com sucesso.'); onClose(); }, onError: (e) => notifyError(e, 'O pagamento não foi registrado') },
      ))}>
        <Stack gap="md">
          <NumberInput label="Valor pago" withAsterisk prefix="R$ " decimalSeparator="," thousandSeparator="." decimalScale={2} allowNegative={false}
            description={expense.amount !== null ? `Valor total da despesa: ${expense.amount.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}` : undefined}
            {...form.getInputProps('paidAmount')} />
          <DateInput label="Data do pagamento" withAsterisk valueFormat="DD/MM/YYYY" {...form.getInputProps('paymentDate')} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={mutation.isPending}>Registrar</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function CancelModal({ expense, opened, onClose }: { expense: Expense; opened: boolean; onClose: () => void }) {
  const mutation = expensesApi.useCancel(expense.id);
  const form = useForm<{ reason: string }>({ initialValues: { reason: '' }, validate: { reason: (v) => (v.trim() ? null : 'Informe o motivo do cancelamento.') } });
  return (
    <Modal opened={opened} onClose={onClose} title={`Cancelar "${expense.description}"?`} centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate(v.reason.trim(), {
        onSuccess: () => { notifySuccess('Despesa cancelada.'); onClose(); },
        onError: (e) => notifyError(e, 'A despesa não foi cancelada'),
      }))}>
        <Stack gap="md">
          <Text size="sm">O registro continua no histórico, marcado como cancelado. Esta ação é definitiva.</Text>
          <Textarea label="Motivo" withAsterisk autosize minRows={2} {...form.getInputProps('reason')} />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Voltar</Button>
            <Button type="submit" color="red" loading={mutation.isPending}>Cancelar despesa</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
