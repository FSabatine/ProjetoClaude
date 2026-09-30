import { Anchor, Badge, Group, Paper, Stack, Text, Title } from '@mantine/core';
import { IconCheck, IconMinus, IconX } from '@tabler/icons-react';
import { Link, useParams } from 'react-router-dom';
import { AttachmentList } from '../../components/Attachments';
import { StatusBadge } from '../../components/common';
import { InfoGrid } from '../../components/DetailLayout';
import { EntityFormPage } from '../../components/EntityFormPage';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { checklistsApi, type ChecklistAnswer, type ChecklistExecution } from '../operations/api';
import { CHECKLIST_CHOICE, CHECKLIST_RESULT, OCCURRENCE_SEVERITY, ODOMETER_STATUS } from '../operations/labels';

export function ChecklistDetailPage() {
  const { id } = useParams();
  const detail = checklistsApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(c) => c && <ChecklistDetail execution={c} />}</EntityFormPage>;
}

/** Read-only: the questions are the snapshot taken at execution time, whatever the template looks like today. */
function ChecklistDetail({ execution: c }: { execution: ChecklistExecution }) {
  const sections = [...new Set(c.answers.map((a) => a.section ?? ''))];
  return (
    <>
      <PageHeader
        title={`${c.templateName} · ${formatPlate(c.licensePlate)}`}
        description={`Realizado em ${formatDateTime(c.performedAt)} por ${c.performedByName ?? 'Sistema'} (modelo versão ${c.templateVersion}).`}
        breadcrumbs={[{ label: 'Checklists', to: '/checklists' }, { label: 'Detalhe' }]}
      />
      <Stack gap="md">
        <Paper p="md">
          <Stack gap="md">
            <Group gap="xs"><StatusBadge value={c.result} map={CHECKLIST_RESULT} />{c.failedItems > 0 && <Text size="sm" c="red">{c.failedItems} item(ns) não conforme(s)</Text>}</Group>
            <InfoGrid items={[
              { label: 'Veículo', value: <Anchor component={Link} to={`/veiculos/${c.vehicleId}?aba=checklists`} ff="monospace" size="sm">{formatPlate(c.licensePlate)}</Anchor> },
              { label: 'Motorista', value: c.driverName },
              { label: 'Hodômetro', value: c.odometerKm !== null && <Group gap={6}>{formatNumber(c.odometerKm)} km {c.odometerStatus && c.odometerStatus !== 'Valid' && <StatusBadge value={c.odometerStatus} map={ODOMETER_STATUS} />}</Group> },
              { label: 'Local', value: c.location },
              { label: 'Observações', value: c.notes },
            ]} />
          </Stack>
        </Paper>
        {sections.map((section) => (
          <Paper key={section} p="md">
            {section && <Title order={4} fz="md" mb="sm">{section}</Title>}
            <Stack gap="sm">
              {c.answers.filter((a) => (a.section ?? '') === section).map((a) => <AnswerRow key={a.id} answer={a} />)}
            </Stack>
          </Paper>
        ))}
      </Stack>
    </>
  );
}

function AnswerRow({ answer: a }: { answer: ChecklistAnswer }) {
  const icon = a.choice === 'Pass' ? <IconCheck size={16} color="var(--mantine-color-teal-6)" /> : a.choice === 'Fail' ? <IconX size={16} color="var(--mantine-color-red-6)" /> : <IconMinus size={16} />;
  const value = a.responseType === 'PassFail'
    ? (a.choice ? CHECKLIST_CHOICE[a.choice].label : 'Não respondido')
    : a.responseType === 'Number' ? (a.numberValue !== null ? `${formatNumber(a.numberValue)} ${a.unit ?? ''}` : '—') : (a.textValue ?? '—');
  return (
    <Stack gap={4}>
      <Group justify="space-between" wrap="nowrap" gap="xs">
        <Group gap="xs" wrap="nowrap">
          {a.responseType === 'PassFail' && icon}
          <Text size="sm">{a.label}</Text>
        </Group>
        <Text size="sm" fw={a.choice === 'Fail' ? 700 : 500} c={a.choice === 'Fail' ? 'red' : undefined} ta="right">{value}</Text>
      </Group>
      {a.choice === 'Fail' && (
        <Group gap="xs" pl={24}>
          {a.severity && <StatusBadge value={a.severity} map={OCCURRENCE_SEVERITY} />}
          {a.occurrenceId && <Anchor component={Link} to={`/ocorrencias/${a.occurrenceId}`} size="xs">Ver ocorrência</Anchor>}
        </Group>
      )}
      {a.comment && <Text size="xs" c="dimmed" pl={24}>{a.comment}</Text>}
      {a.files.length > 0 && <div style={{ paddingLeft: 24 }}><AttachmentList files={a.files} /></div>}
      {!a.isRequired && a.responseType !== 'PassFail' && !a.numberValue && !a.textValue && <Badge variant="default" size="xs" ml={24}>opcional</Badge>}
    </Stack>
  );
}
