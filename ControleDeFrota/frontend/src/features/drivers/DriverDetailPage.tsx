import { Alert, Anchor, Button, Group, Paper, SimpleGrid, Stack, Text } from '@mantine/core';
import { IconAlertTriangle, IconChecklist, IconFileText, IconHistory, IconInfoCircle, IconPencil, IconTruck } from '@tabler/icons-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { DetailTabs, HeaderFact, InfoGrid, Section, type DetailTab } from '../../components/DetailLayout';
import { EntityFormPage } from '../../components/EntityFormPage';
import { PageHeader } from '../../components/PageHeader';
import { formatCpf, formatDate, formatPhone, formatPlate, formatZipCode } from '../../lib/format';
import { dueLabel } from '../../lib/mileage';
import { DriverAssignmentPanel } from '../assignments/AssignmentPanel';
import { AuditHistoryButton } from '../audit/AuditHistoryButton';
import { ChecklistsPanel } from '../checklists/ChecklistsPanel';
import { DocumentsPanel } from '../documents/DocumentsPanel';
import { HistoryTimeline } from '../history/HistoryTimeline';
import { OccurrencesPanel } from '../occurrences/OccurrencesPanel';
import { documentsApi, occurrencesApi } from '../operations/api';
import { DRIVER_STATUS, LICENSE_STATE, driversApi, type Driver } from './drivers';

export function DriverDetailPage() {
  const { id } = useParams();
  const detail = driversApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(d) => d && <DriverHub driver={d} />}</EntityFormPage>;
}

function DriverHub({ driver: d }: { driver: Driver }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const openOccurrences = occurrencesApi.useList({ driverId: d.id, openOnly: 'true', pageSize: 1 }, can(PERMISSIONS.occurrences.view));

  const tabs: DetailTab[] = [
    { value: 'visao-geral', label: 'Visão geral', icon: <IconInfoCircle size={16} />, content: <Overview driver={d} /> },
    ...(can(PERMISSIONS.assignments.view) ? [{
      value: 'veiculos', label: 'Veículos', icon: <IconTruck size={16} />,
      content: <DriverAssignmentPanel driverId={d.id} current={d.currentVehicle} />,
    }] : []),
    ...(can(PERMISSIONS.documents.view) ? [{ value: 'documentos', label: 'Documentos', icon: <IconFileText size={16} />, content: <DocumentsPanel ownerType="Driver" ownerId={d.id} /> }] : []),
    ...(can(PERMISSIONS.checklists.view) ? [{ value: 'checklists', label: 'Checklists', icon: <IconChecklist size={16} />, content: <ChecklistsPanel filter={{ driverId: d.id }} /> }] : []),
    ...(can(PERMISSIONS.occurrences.view) ? [{
      value: 'ocorrencias', label: 'Ocorrências', icon: <IconAlertTriangle size={16} />, count: openOccurrences.data?.totalCount,
      content: <OccurrencesPanel filter={{ driverId: d.id }} defaults={{ driverId: d.id, driverLabel: d.fullName, vehicleId: d.currentVehicle?.vehicleId, vehicleLabel: d.currentVehicle && formatPlate(d.currentVehicle.licensePlate) }} />,
    }] : []),
    { value: 'historico', label: 'Histórico', icon: <IconHistory size={16} />, content: <HistoryTimeline owner="drivers" id={d.id} /> },
  ];

  return (
    <>
      <PageHeader
        title={d.fullName}
        description={`CNH categoria ${d.licenseCategory}`}
        breadcrumbs={[{ label: 'Motoristas', to: '/motoristas' }, { label: d.fullName }]}
        action={
          <Group gap="xs">
            <AuditHistoryButton entity="Driver" id={d.id} />
            <Button variant="default" leftSection={<IconPencil size={18} />} onClick={() => navigate(`/motoristas/${d.id}/editar`)}>
              {can(PERMISSIONS.drivers.update) ? 'Editar' : 'Ver cadastro'}
            </Button>
          </Group>
        }
      />
      <Paper p="md" mb="md">
        <SimpleGrid cols={{ base: 2, md: 4 }} spacing="md">
          <HeaderFact label="Situação" value={<StatusBadge value={d.status} map={DRIVER_STATUS} />} />
          <HeaderFact label="CNH" value={<StatusBadge value={d.licenseState} map={LICENSE_STATE} />} hint={`validade ${formatDate(d.licenseExpiresOn)}`} />
          <HeaderFact label="Categoria" value={d.licenseCategory} hint={d.performsPaidActivity ? 'EAR' : undefined} />
          <HeaderFact label="Veículo atual"
            value={d.currentVehicle ? <Anchor component={Link} to={`/veiculos/${d.currentVehicle.vehicleId}`} ff="monospace" fw={700}>{formatPlate(d.currentVehicle.licensePlate)}</Anchor> : 'Sem veículo'}
            hint={d.currentVehicle?.vehicleDescription} />
        </SimpleGrid>
      </Paper>
      <DetailTabs tabs={tabs} />
    </>
  );
}

function Overview({ driver: d }: { driver: Driver }) {
  const { can } = useAuth();
  const documents = documentsApi.useList({ driverId: d.id, alertsOnly: 'true', pageSize: 5 }, can(PERMISSIONS.documents.view));
  const alerts = [
    ...(d.licenseState !== 'Valid' ? [{ key: 'cnh', color: d.licenseState === 'Expired' ? 'red' : 'orange',
      text: d.licenseState === 'Expired' ? 'CNH vencida: o motorista não pode receber veículo.' : `CNH vence em ${formatDate(d.licenseExpiresOn)}.` }] : []),
    ...(documents.data?.items ?? []).map((doc) => ({ key: doc.id, color: doc.status === 'Expired' ? 'red' : 'orange', text: `${doc.documentTypeName} ${dueLabel(doc.daysUntilExpiration)}.` })),
  ];
  const address = [d.address.street, d.address.number, d.address.complement, d.address.neighborhood].filter(Boolean).join(', ');

  return (
    <Stack gap="md">
      {alerts.length > 0
        ? alerts.map((a) => <Alert key={a.key} color={a.color} icon={<IconAlertTriangle size={18} />} p="xs"><Text size="sm">{a.text}</Text></Alert>)
        : <Alert color="teal" icon={<IconInfoCircle />} p="xs">Nenhum alerta para este motorista.</Alert>}
      <Section title="Dados pessoais">
        <InfoGrid items={[
          { label: 'CPF', value: formatCpf(d.cpf) },
          { label: 'RG', value: d.rg },
          { label: 'Nascimento', value: formatDate(d.birthDate) },
          { label: 'Telefone', value: formatPhone(d.phone) },
          { label: 'E-mail', value: d.email },
          { label: 'Endereço', value: address ? `${address} — ${d.address.city ?? ''}/${d.address.state ?? ''} ${formatZipCode(d.address.zipCode)}` : null },
        ]} />
      </Section>
      <Section title="Habilitação">
        <InfoGrid items={[
          { label: 'Número da CNH', value: d.licenseNumber },
          { label: 'Categoria', value: d.licenseCategory },
          { label: 'Validade', value: formatDate(d.licenseExpiresOn) },
          { label: 'EAR', value: d.performsPaidActivity ? 'Sim' : 'Não' },
        ]} />
      </Section>
      {d.notes && <Section title="Observações"><Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>{d.notes}</Text></Section>}
    </Stack>
  );
}
