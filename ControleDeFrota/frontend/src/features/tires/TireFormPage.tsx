import { useEffect, useState } from 'react';
import { Anchor, Box, Button, Checkbox, Group, Modal, NumberInput, Select, SimpleGrid, Stack, Text, TextInput, Textarea } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { toSelectData } from '../../components/common';
import { EntityFormPage } from '../../components/EntityFormPage';
import { FormActions, FormSection, useUnsavedChangesGuard } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { submitWithFeedback } from '../../components/useApiForm';
import { fromApiDate, toApiDate } from '../../lib/format';
import { dotManufacture, isoWeekMonday } from '../../lib/tires';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { InfoHint } from '../fuel/components';
import type { FileInfo } from '../operations/api';
import { tireModelsApi, tiresApi } from './api';
import { TIRE_APPLICATION, TIRE_CONSTRUCTION, type Tire, type TireModel } from './tires';

type NumberValue = number | string;
const toNumber = (v: NumberValue) => (v === '' || v === null ? null : Number(v));

export const FIRE_NUMBER_HELP = (
  <>O <b>número de fogo</b> é o código que identifica cada pneu da frota, marcado na lateral. Ele acompanha o pneu em todos os veículos
    por onde passar. Deixe em branco para o sistema gerar o próximo (PN-000001…).</>
);

export const DOT_HELP = (
  <>O <b>DOT</b> está gravado na lateral do pneu. Os quatro últimos números são a semana e o ano de fabricação: “3524” = semana 35 de 2024.
    A data de fabricação é preenchida a partir dele.</>
);

export function TireFormPage() {
  const { id } = useParams();
  const detail = tiresApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(tire) => <TireForm tire={tire} />}</EntityFormPage>;
}

function TireForm({ tire }: { tire?: Tire }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const save = tiresApi.useSave(tire?.id);
  const models = tireModelsApi.useList(false);
  const [modelModal, setModelModal] = useState(false);
  const [files, setFiles] = useState<FileInfo[]>([]);
  const canSeeCosts = can(PERMISSIONS.tires.viewcosts);
  const readOnly = !can(tire ? PERMISSIONS.tires.edit : PERMISSIONS.tires.create) || (!!tire && !tire.actions.canEdit);

  const form = useForm({
    initialValues: {
      code: tire?.code ?? '',
      tireModelId: (tire?.tireModelId ?? null) as string | null,
      serialNumber: tire?.serialNumber ?? '',
      dot: tire?.dot ?? '',
      manufacturedOn: fromApiDate(tire?.manufacturedOn),
      purchasedOn: fromApiDate(tire?.purchasedOn) ?? (tire ? null : new Date()),
      purchasePrice: (tire?.purchasePrice ?? '') as NumberValue,
      supplier: tire?.supplier ?? '',
      originalTreadDepthMm: (tire?.originalTreadDepthMm ?? '') as NumberValue,
      usedTire: false,
      currentTreadDepthMm: '' as NumberValue,
      retreadCount: 0 as NumberValue,
      storageLocation: tire?.storageLocation ?? '',
      notes: tire?.notes ?? '',
    },
    validateInputOnBlur: true,
    validate: {
      tireModelId: (v: string | null) => (v ? null : 'Modelo: campo obrigatório.'),
      purchasePrice: (v: NumberValue) => (v !== '' && Number(v) < 0 ? 'Valor inválido.' : null),
    },
    onValuesChange: (values, previous) => {
      // The model brings its original tread; the DOT brings the manufacturing date — both remain editable.
      if (values.tireModelId !== previous.tireModelId) {
        const model = models.data?.find((m) => m.id === values.tireModelId);
        if (model?.originalTreadDepthMm && values.originalTreadDepthMm === '') form.setFieldValue('originalTreadDepthMm', model.originalTreadDepthMm);
      }
      if (values.dot !== previous.dot) {
        const made = dotManufacture(values.dot);
        if (made) form.setFieldValue('manufacturedOn', isoWeekMonday(made.year, made.week));
      }
    },
  });
  const guard = useUnsavedChangesGuard(form.isDirty());
  const made = dotManufacture(form.values.dot);

  const handleSubmit = submitWithFeedback({
    form,
    mutation: save,
    successMessage: tire ? 'Pneu atualizado com sucesso.' : 'Pneu cadastrado com sucesso.',
    errorTitle: 'O pneu não foi salvo',
    onSuccess: (saved) => {
      guard.release();
      navigate(`/pneus/${saved.id}`);
    },
    toBody: (v) => ({
      code: v.code || null,
      tireModelId: v.tireModelId,
      serialNumber: v.serialNumber || null,
      dot: v.dot || null,
      manufacturedOn: toApiDate(v.manufacturedOn),
      purchasedOn: toApiDate(v.purchasedOn),
      purchasePrice: canSeeCosts ? toNumber(v.purchasePrice) : null,
      supplier: v.supplier || null,
      originalTreadDepthMm: toNumber(v.originalTreadDepthMm),
      currentTreadDepthMm: v.usedTire ? toNumber(v.currentTreadDepthMm) : null,
      retreadCount: v.usedTire ? Number(v.retreadCount || 0) : 0,
      storageLocation: v.storageLocation || null,
      notes: v.notes || null,
      fileIds: files.map((f) => f.id),
    }),
  });

  const modelOptions = (models.data ?? []).map((m) => ({ value: m.id, label: `${m.brand} ${m.name} · ${m.size}` }));
  if (tire && !modelOptions.some((o) => o.value === tire.tireModelId))
    modelOptions.unshift({ value: tire.tireModelId, label: `${tire.brand} ${tire.modelName} · ${tire.size}` });

  return (
    <>
      <PageHeader
        title={tire ? `Pneu ${tire.code}` : 'Novo pneu'}
        description={readOnly ? 'Visualização — este cadastro não pode ser editado por você.' : 'Campos com * são obrigatórios.'}
        breadcrumbs={[{ label: 'Pneus', to: '/pneus' }, ...(tire ? [{ label: tire.code, to: `/pneus/${tire.id}` }] : []), { label: tire ? 'Editar' : 'Novo' }]}
        action={tire && <AuditHistoryButton entity="Tire" id={tire.id} />}
      />
      <form onSubmit={handleSubmit} noValidate>
        <fieldset disabled={readOnly} style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}>
          <Stack gap="md">
            <FormSection title="Identificação" description="Cada pneu tem identidade própria: ele pode passar por vários veículos." cols={3}>
              <TextInput label={<Group gap={2} component="span">Número de fogo <InfoHint label="Número de fogo">{FIRE_NUMBER_HELP}</InfoHint></Group>}
                placeholder="Gerado automaticamente" maxLength={20} data-autofocus {...form.getInputProps('code')} />
              <Stack gap={2}>
                <Select label="Modelo" withAsterisk searchable data={modelOptions} placeholder="Marca, modelo e medida" {...form.getInputProps('tireModelId')} />
                {can(PERMISSIONS.tires.create) && <Anchor component="button" type="button" size="xs" onClick={() => setModelModal(true)}>Cadastrar novo modelo</Anchor>}
              </Stack>
              <TextInput label="Número de série" maxLength={40} {...form.getInputProps('serialNumber')} />
              <TextInput label={<Group gap={2} component="span">DOT <InfoHint label="DOT">{DOT_HELP}</InfoHint></Group>} placeholder="Ex.: DOT 4B7X 3524"
                maxLength={20} description={made ? `Fabricado na semana ${made.week} de ${made.year}` : undefined} {...form.getInputProps('dot')} />
              <DateInput label="Data de fabricação" valueFormat="DD/MM/YYYY" clearable maxDate={new Date()} {...form.getInputProps('manufacturedOn')} />
              <NumberInput label="Sulco original (mm)" decimalScale={1} decimalSeparator="," allowNegative={false} max={40} {...form.getInputProps('originalTreadDepthMm')} />
            </FormSection>

            <FormSection title="Compra" cols={3}>
              <DateInput label="Data de compra" valueFormat="DD/MM/YYYY" clearable maxDate={new Date()} {...form.getInputProps('purchasedOn')} />
              {canSeeCosts && (
                <NumberInput label="Valor de compra" prefix="R$ " decimalScale={2} fixedDecimalScale thousandSeparator="." decimalSeparator=","
                  allowNegative={false} {...form.getInputProps('purchasePrice')} />
              )}
              <TextInput label="Fornecedor" maxLength={120} {...form.getInputProps('supplier')} />
            </FormSection>

            {!tire && (
              <FormSection title="Pneu usado" description="Para cadastrar pneus que já estavam em uso antes do sistema." cols={3}>
                <Checkbox label="Este pneu já foi usado" {...form.getInputProps('usedTire', { type: 'checkbox' })} mt="lg" />
                {form.values.usedTire && (
                  <>
                    <NumberInput label="Sulco atual (mm)" decimalScale={1} decimalSeparator="," allowNegative={false} max={40} {...form.getInputProps('currentTreadDepthMm')} />
                    <NumberInput label="Recapagens já feitas" allowNegative={false} allowDecimal={false} min={0} max={10} {...form.getInputProps('retreadCount')} />
                  </>
                )}
              </FormSection>
            )}

            <FormSection title="Armazenamento e observações" cols={2}>
              <TextInput label="Local de armazenamento" placeholder="Ex.: Almoxarifado A · Prateleira 3 · Posição 12" maxLength={120}
                disabled={tire?.status === 'Installed'} description={tire?.status === 'Installed' ? 'Pneu instalado: o local é a posição no veículo.' : undefined}
                {...form.getInputProps('storageLocation')} />
              <Textarea label="Observações" autosize minRows={2} maxLength={2000} {...form.getInputProps('notes')} />
            </FormSection>

            {!tire && (
              <FormSection title="Documentos" description="Nota fiscal, garantia ou foto do pneu (PDF, JPG ou PNG)." cols={1}>
                <Group gap="sm"><UploadButton onUploaded={(f) => setFiles((x) => [...x, f])} /><UploadButton camera onUploaded={(f) => setFiles((x) => [...x, f])} /></Group>
                {files.length > 0 && <AttachmentList files={files} onRemove={(f) => setFiles((x) => x.filter((y) => y.id !== f.id))} />}
              </FormSection>
            )}
          </Stack>
        </fieldset>
        {!readOnly && (
          <Box mt="md">
            <FormActions saving={save.isPending} onCancel={() => navigate(tire ? `/pneus/${tire.id}` : '/pneus')} submitLabel={tire ? 'Salvar alterações' : 'Cadastrar pneu'} />
          </Box>
        )}
      </form>
      <TireModelFormModal opened={modelModal} onClose={() => setModelModal(false)} onSaved={(m) => form.setFieldValue('tireModelId', m.id)} />
    </>
  );
}

/** Catalog entry (brand + model + size + specs) — used by the tire form and the settings page. */
export function TireModelFormModal({ opened, onClose, model, onSaved }: { opened: boolean; onClose: () => void; model?: TireModel; onSaved?: (m: TireModel) => void }) {
  const save = tireModelsApi.useSave(model?.id);
  const blank = { brand: '', name: '', size: '', application: 'AllPosition', construction: 'Radial', loadIndex: '', speedRating: '', originalTreadDepthMm: '' as NumberValue, isActive: true, notes: '' };
  const [v, setV] = useState(blank);
  const [errors, setErrors] = useState<Record<string, string>>({});
  useEffect(() => {
    if (!opened) return;
    setErrors({});
    setV(model ? {
      brand: model.brand, name: model.name, size: model.size, application: model.application, construction: model.construction, loadIndex: model.loadIndex ?? '',
      speedRating: model.speedRating ?? '', originalTreadDepthMm: model.originalTreadDepthMm ?? '', isActive: model.isActive, notes: model.notes ?? '',
    } : blank);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset when opened
  }, [opened, model]);

  const submit = () =>
    save.mutate({
      brand: v.brand, name: v.name, size: v.size, application: v.application as TireModel['application'], construction: v.construction as TireModel['construction'],
      loadIndex: v.loadIndex || null, speedRating: v.speedRating || null, originalTreadDepthMm: toNumber(v.originalTreadDepthMm), isActive: v.isActive, notes: v.notes || null,
    }, {
      onSuccess: (saved) => { notifySuccess('Modelo salvo.'); onSaved?.(saved); onClose(); },
      onError: (e) => setErrors(notifyError(e, 'O modelo não foi salvo').fieldErrors),
    });

  return (
    <Modal opened={opened} onClose={onClose} title={model ? 'Editar modelo de pneu' : 'Novo modelo de pneu'} size="lg" centered>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <Stack>
          <SimpleGrid cols={{ base: 1, sm: 3 }}>
            <TextInput label="Marca" withAsterisk maxLength={60} value={v.brand} onChange={(e) => setV({ ...v, brand: e.currentTarget.value })} error={errors.brand} data-autofocus />
            <TextInput label="Modelo" withAsterisk maxLength={80} value={v.name} onChange={(e) => setV({ ...v, name: e.currentTarget.value })} error={errors.name} />
            <TextInput label="Medida" withAsterisk placeholder="295/80R22.5" maxLength={30} value={v.size} onChange={(e) => setV({ ...v, size: e.currentTarget.value })}
              error={errors.size} disabled={!!model && model.tireCount > 0} description={model && model.tireCount > 0 ? 'Há pneus deste modelo.' : undefined} />
            <Select label="Aplicação" data={toSelectData(TIRE_APPLICATION)} value={v.application} allowDeselect={false} onChange={(x) => setV({ ...v, application: x ?? 'AllPosition' })} />
            <Select label="Construção" data={toSelectData(TIRE_CONSTRUCTION)} value={v.construction} allowDeselect={false} onChange={(x) => setV({ ...v, construction: x ?? 'Radial' })} />
            <NumberInput label="Sulco original (mm)" decimalScale={1} decimalSeparator="," allowNegative={false} value={v.originalTreadDepthMm}
              onChange={(x) => setV({ ...v, originalTreadDepthMm: x })} error={errors.originalTreadDepthMm} />
            <TextInput label="Índice de carga" placeholder="152/148" maxLength={10} value={v.loadIndex} onChange={(e) => setV({ ...v, loadIndex: e.currentTarget.value })} />
            <TextInput label="Índice de velocidade" placeholder="M" maxLength={10} value={v.speedRating} onChange={(e) => setV({ ...v, speedRating: e.currentTarget.value })} />
            <Checkbox label="Ativo" checked={v.isActive} onChange={(e) => setV({ ...v, isActive: e.currentTarget.checked })} mt="xl" />
          </SimpleGrid>
          <Textarea label="Observações" autosize minRows={2} maxLength={500} value={v.notes} onChange={(e) => setV({ ...v, notes: e.currentTarget.value })} />
          <Text size="xs" c="dimmed">Carga e velocidade são informativas: o sistema não verifica essas exigências por posição.</Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>Voltar</Button>
            <Button type="submit" loading={save.isPending}>Salvar modelo</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
