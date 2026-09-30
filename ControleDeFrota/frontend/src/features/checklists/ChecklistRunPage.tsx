import { useEffect, useMemo, useState } from 'react';
import {
  Alert, Badge, Button, Chip, Group, NumberInput, Paper, Progress, Select, SimpleGrid, Stack, Text, Textarea, TextInput, ThemeIcon, Title,
} from '@mantine/core';
import { IconAlertTriangle, IconCheck, IconChecks, IconCircleCheck, IconX } from '@tabler/icons-react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { toApiError } from '../../api/errors';
import { AttachmentList, UploadButton } from '../../components/Attachments';
import { DriverPicker, VehiclePicker } from '../../components/EntityPickers';
import { useUnsavedChangesGuard } from '../../components/forms';
import { notifyError } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { ErrorState } from '../../components/States';
import { formatNumber, formatPlate } from '../../lib/format';
import { odometerHint } from '../../lib/mileage';
import {
  checklistTemplatesApi, checklistsApi, type ChecklistExecution, type ChecklistTemplate, type ChecklistTemplateItem, type FileInfo,
} from '../operations/api';
import { CHECKLIST_CHOICE, OCCURRENCE_SEVERITY, type ChecklistChoice, type OccurrenceSeverity } from '../operations/labels';
import { vehiclesApi } from '../vehicles/vehicles';

interface AnswerState {
  choice: ChecklistChoice | null;
  numberValue: number | string;
  textValue: string;
  comment: string;
  severity: OccurrenceSeverity | null;
  files: FileInfo[];
}

const emptyAnswer = (): AnswerState => ({ choice: null, numberValue: '', textValue: '', comment: '', severity: null, files: [] });

const isAnswered = (item: ChecklistTemplateItem, a: AnswerState) =>
  item.responseType === 'PassFail' ? a.choice !== null : item.responseType === 'Number' ? a.numberValue !== '' : a.textValue.trim() !== '';

/**
 * Inspection on the phone (UX_UI.md, "Checklist no celular"): pick vehicle and model, tap answers, photograph problems,
 * submit. Every failed item becomes an operational occurrence on the server.
 */
export function ChecklistRunPage() {
  const [params] = useSearchParams();
  const [vehicleId, setVehicleId] = useState<string | null>(params.get('veiculo'));
  const [templateId, setTemplateId] = useState<string | null>(params.get('modelo'));
  const [done, setDone] = useState<ChecklistExecution | null>(null);
  const vehicle = vehiclesApi.useDetail(vehicleId ?? undefined);
  const templates = checklistTemplatesApi.useList();
  const template = checklistTemplatesApi.useDetail(templateId);

  // One active model (the usual case) is chosen automatically: one tap less.
  useEffect(() => {
    if (!templateId && templates.data?.length === 1) setTemplateId(templates.data[0].id);
  }, [templateId, templates.data]);

  if (done) return <ChecklistDone execution={done} onAnother={() => { setDone(null); setVehicleId(null); }} />;

  return (
    <>
      <PageHeader title="Realizar checklist" description="Inspecione o veículo antes do uso. Itens não conformes geram ocorrências automaticamente."
        breadcrumbs={[{ label: 'Checklists', to: '/checklists' }, { label: 'Realizar' }]} />
      <Stack gap="md">
        <Paper p="md">
          <Stack gap="sm">
            <VehiclePicker label="Veículo" withAsterisk size="md" value={vehicleId} onChange={setVehicleId}
              initialLabel={vehicle.data ? `${formatPlate(vehicle.data.licensePlate)} · ${vehicle.data.manufacturer} ${vehicle.data.model}` : null} />
            <Select label="Modelo de checklist" withAsterisk size="md" value={templateId} onChange={setTemplateId}
              data={(templates.data ?? []).map((t) => ({ value: t.id, label: `${t.name} (${t.itemCount} itens)` }))}
              nothingFoundMessage="Nenhum modelo ativo. Cadastre em Configurações › Modelos de checklist." />
          </Stack>
        </Paper>
        {template.error && <ErrorState error={template.error} onRetry={() => void template.refetch()} />}
        {vehicle.data && template.data && (
          <ChecklistForm key={`${vehicle.data.id}-${template.data.id}-${template.data.version}`} template={template.data}
            vehicle={{ id: vehicle.data.id, plate: vehicle.data.licensePlate, km: vehicle.data.currentOdometerKm,
              kmAt: vehicle.data.odometerUpdatedAt, driverId: vehicle.data.currentAssignment?.driverId ?? null,
              driverName: vehicle.data.currentAssignment?.driverName ?? null }}
            onDone={setDone} />
        )}
      </Stack>
    </>
  );
}

function ChecklistForm({ template, vehicle, onDone }: {
  template: ChecklistTemplate;
  vehicle: { id: string; plate: string; km: number; kmAt: string | null; driverId: string | null; driverName: string | null };
  onDone: (e: ChecklistExecution) => void;
}) {
  const submit = checklistsApi.useSubmit();
  const [answers, setAnswers] = useState<Record<string, AnswerState>>(() => Object.fromEntries(template.items.map((i) => [i.id, emptyAnswer()])));
  const [odometer, setOdometer] = useState<number | string>('');
  const [driverId, setDriverId] = useState<string | null>(vehicle.driverId);
  const [location, setLocation] = useState('');
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});

  const answered = template.items.filter((i) => isAnswered(i, answers[i.id])).length;
  const dirty = answered > 0 || odometer !== '';
  const guard = useUnsavedChangesGuard(dirty && !submit.isSuccess);
  const failures = template.items.filter((i) => answers[i.id].choice === 'Fail').length;
  const hint = odometerHint(odometer === '' ? null : Number(odometer), vehicle.km, vehicle.kmAt ? new Date(vehicle.kmAt) : null, new Date());

  const sections = useMemo(() => {
    const map = new Map<string, ChecklistTemplateItem[]>();
    for (const item of template.items) map.set(item.section ?? '', [...(map.get(item.section ?? '') ?? []), item]);
    return [...map.entries()];
  }, [template.items]);

  const set = (id: string, change: Partial<AnswerState>) => {
    setAnswers((all) => ({ ...all, [id]: { ...all[id], ...change } }));
    setErrors((e) => ({ ...e, [`answers.${id}`]: '' }));
  };

  const allPass = () =>
    setAnswers((all) => Object.fromEntries(template.items.map((i) => [i.id,
      i.responseType === 'PassFail' && all[i.id].choice === null ? { ...all[i.id], choice: 'Pass' as const } : all[i.id]])));

  const send = () =>
    submit.mutate({
      vehicleId: vehicle.id, templateId: template.id, templateVersion: template.version, driverId,
      odometerKm: odometer === '' ? null : Number(odometer), location: location || null, notes: notes || null,
      answers: template.items.map((i) => {
        const a = answers[i.id];
        return {
          templateItemId: i.id, choice: a.choice, numberValue: a.numberValue === '' ? null : Number(a.numberValue),
          textValue: a.textValue || null, comment: a.comment || null, severity: a.choice === 'Fail' ? a.severity : null, fileIds: a.files.map((f) => f.id),
        };
      }),
    }, {
      onSuccess: (execution) => { guard.release(); onDone(execution); },
      onError: (e) => {
        const apiError = toApiError(e);
        setErrors(apiError.fieldErrors);
        notifyError(e, 'O checklist não foi enviado');
        // Bring the first problem into view — on a phone it is usually off-screen.
        const first = Object.keys(apiError.fieldErrors)[0];
        if (first) document.getElementById(first)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      },
    });

  return (
    <Stack gap="md" pb={96}>
      <Paper p="md">
        <Stack gap="sm">
          <DriverPicker label="Motorista" value={driverId} onChange={setDriverId} excludeInactive={false} initialLabel={vehicle.driverName}
            description={vehicle.driverName ? 'Preenchido com o motorista alocado.' : undefined} />
          <NumberInput id="odometerKm" label="Hodômetro (opcional)" size="md" suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
            inputMode="numeric" value={odometer} onChange={setOdometer} error={errors.odometerKm}
            description={`Última leitura: ${formatNumber(vehicle.km)} km`} />
          {hint.kind === 'decrease' && <Text size="sm" c="red">Menor que a última leitura — confira o valor.</Text>}
          {hint.kind === 'suspicious' && <Text size="sm" c="orange">Aumento de {formatNumber(hint.deltaKm)} km: a leitura ficará em revisão.</Text>}
        </Stack>
      </Paper>

      <Group justify="space-between">
        <Text size="sm" c="dimmed">{answered} de {template.items.length} respondidos</Text>
        <Button variant="light" leftSection={<IconChecks size={18} />} onClick={allPass}>Marcar restantes como conforme</Button>
      </Group>
      <Progress value={(answered / template.items.length) * 100} size="sm" aria-label="Progresso do checklist" />

      {sections.map(([section, items]) => (
        <Stack key={section} gap="xs">
          {section && <Title order={4} fz="md" mt="xs">{section}</Title>}
          {items.map((item) => (
            <ItemCard key={item.id} item={item} answer={answers[item.id]} error={errors[`answers.${item.id}`]} onChange={(c) => set(item.id, c)} />
          ))}
        </Stack>
      ))}

      <Paper p="md">
        <Stack gap="sm">
          <TextInput label="Local" placeholder="Ex.: Pátio Curitiba" maxLength={200} value={location} onChange={(e) => setLocation(e.currentTarget.value)} />
          <Textarea label="Observações gerais" autosize minRows={2} maxLength={1000} value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />
        </Stack>
      </Paper>

      <Paper p="sm" shadow="md" style={{ position: 'sticky', bottom: 'var(--mantine-spacing-md)', zIndex: 5 }}>
        <Group justify="space-between" wrap="nowrap">
          <Text size="sm">{failures > 0 ? <Text span c="red" fw={600}>{failures} não conforme(s)</Text> : `${answered}/${template.items.length}`}</Text>
          <Button size="md" onClick={send} loading={submit.isPending} leftSection={<IconCheck size={18} />}>Enviar checklist</Button>
        </Group>
      </Paper>
    </Stack>
  );
}

function ItemCard({ item, answer, error, onChange }: {
  item: ChecklistTemplateItem;
  answer: AnswerState;
  error?: string;
  onChange: (change: Partial<AnswerState>) => void;
}) {
  const failed = answer.choice === 'Fail';
  return (
    <Paper id={`answers.${item.id}`} p="md" withBorder style={{ borderColor: error ? 'var(--mantine-color-red-6)' : failed ? 'var(--mantine-color-red-3)' : undefined }}>
      <Stack gap="sm">
        <Group justify="space-between" wrap="nowrap" align="flex-start">
          <Text fw={500}>{item.label}{item.isRequired && <Text span c="red"> *</Text>}</Text>
          {item.unit && <Badge variant="default">{item.unit}</Badge>}
        </Group>

        {item.responseType === 'PassFail' && (
          // Three big buttons (≥48px) instead of a segmented control: readable and tappable at 375px.
          <SimpleGrid cols={3} spacing={6} role="radiogroup" aria-label={item.label}>
            {(Object.keys(CHECKLIST_CHOICE) as ChecklistChoice[]).map((c) => {
              const selected = answer.choice === c;
              return (
                <Button
                  key={c}
                  role="radio"
                  aria-checked={selected}
                  h={48}
                  px={4}
                  variant={selected ? 'filled' : 'default'}
                  color={CHECKLIST_CHOICE[c].color}
                  leftSection={c === 'Pass' ? <IconCheck size={16} /> : c === 'Fail' ? <IconX size={16} /> : undefined}
                  styles={{ label: { whiteSpace: 'normal', lineHeight: 1.15, textAlign: 'center' }, section: { marginInlineEnd: 4 } }}
                  onClick={() => onChange({ choice: c, severity: c === 'Fail' ? answer.severity ?? item.failureSeverity : null })}
                >
                  {c === 'NotApplicable' ? 'N/A' : CHECKLIST_CHOICE[c].label}
                </Button>
              );
            })}
          </SimpleGrid>
        )}
        {item.responseType === 'Number' && (
          <NumberInput size="md" inputMode="decimal" thousandSeparator="." decimalSeparator="," value={answer.numberValue}
            onChange={(v) => onChange({ numberValue: v })} aria-label={item.label} />
        )}
        {item.responseType === 'Text' && (
          <Textarea autosize minRows={2} maxLength={500} value={answer.textValue} onChange={(e) => onChange({ textValue: e.currentTarget.value })} aria-label={item.label} />
        )}

        {failed && (
          <Stack gap="xs">
            <Chip.Group value={answer.severity ?? item.failureSeverity} onChange={(v) => onChange({ severity: v as OccurrenceSeverity })}>
              <Group gap={6}>
                {(Object.keys(OCCURRENCE_SEVERITY) as OccurrenceSeverity[]).map((s) => (
                  <Chip key={s} value={s} size="sm" color={OCCURRENCE_SEVERITY[s].color}>{OCCURRENCE_SEVERITY[s].label}</Chip>
                ))}
              </Group>
            </Chip.Group>
            <Textarea placeholder="Descreva o problema (opcional)" autosize minRows={1} maxLength={500} value={answer.comment}
              onChange={(e) => onChange({ comment: e.currentTarget.value })} aria-label={`Problema em ${item.label}`} />
            <AttachmentList files={answer.files} onRemove={(f) => onChange({ files: answer.files.filter((x) => x.id !== f.id) })} />
            <Group gap="xs">
              <UploadButton camera onUploaded={(f) => onChange({ files: [...answer.files, f] })} label={item.requiresPhotoOnFail ? 'Tirar foto (obrigatória)' : 'Tirar foto'} />
            </Group>
          </Stack>
        )}
        {error && <Text size="sm" c="red">{error}</Text>}
      </Stack>
    </Paper>
  );
}

function ChecklistDone({ execution, onAnother }: { execution: ChecklistExecution; onAnother: () => void }) {
  const navigate = useNavigate();
  const approved = execution.result === 'Approved';
  return (
    <Paper p="xl" maw={560} mx="auto" mt="lg">
      <Stack align="center" ta="center" gap="md">
        <ThemeIcon size={64} radius="xl" color={approved ? 'teal' : 'orange'} variant="light">
          {approved ? <IconCircleCheck size={36} /> : <IconAlertTriangle size={36} />}
        </ThemeIcon>
        <Title order={3}>{approved ? 'Checklist aprovado' : 'Checklist enviado com não conformidades'}</Title>
        <Text c="dimmed">
          {formatPlate(execution.licensePlate)} · {execution.templateName}
          {!approved && ` — ${execution.failedItems} ocorrência(s) aberta(s) para acompanhamento.`}
        </Text>
        {execution.odometerStatus === 'PendingReview' && (
          <Alert color="orange" icon={<IconAlertTriangle />}>O hodômetro informado ficou em revisão por parecer fora do normal.</Alert>
        )}
        <Group justify="center">
          <Button variant="default" onClick={onAnother}>Novo checklist</Button>
          <Button onClick={() => navigate(`/checklists/${execution.id}`)}>Ver checklist</Button>
        </Group>
        <Link to={`/veiculos/${execution.vehicleId}`}>Voltar ao veículo</Link>
      </Stack>
    </Paper>
  );
}
