import { lazy, Suspense, useState } from 'react';
import {
  Alert, Badge, Button, Code, CopyButton, Group, Menu, Modal, Paper, Select, SimpleGrid, Skeleton, Stack, Switch, Table, Text, TextInput, Textarea, Title,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconArrowRight, IconCheck, IconCopy, IconKey, IconPlugConnected, IconPlus, IconRouter } from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { RowActions, toSelectData } from '../../components/common';
import { VehiclePicker } from '../../components/EntityPickers';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime, formatPlate } from '../../lib/format';
import {
  CONNECTION_STATUS, INTEGRATION_STATE, PROVIDER_KIND, trackingApi, type ProviderKind, type TrackingDevice, type TrackingProvider,
} from './tracking';

// Leaflet only loads on the screens that draw a map.
const FleetMap = lazy(() => import('./maps').then((m) => ({ default: m.FleetMap })));
const VehicleLocation = lazy(() => import('./maps').then((m) => ({ default: m.VehicleLocationPanel })));

export function FleetMapPage() {
  const { can } = useAuth();
  const navigate = useNavigate();
  return (
    <>
      <PageHeader title="Mapa da frota" description="Última posição enviada pelos rastreadores. Atualiza a cada minuto."
        action={can(PERMISSIONS.tracking.manage) && (
          <Button variant="default" leftSection={<IconRouter size={18} />} onClick={() => navigate('/configuracoes/rastreadores')}>Rastreadores</Button>
        )} />
      <Suspense fallback={<Skeleton height={560} />}><FleetMap /></Suspense>
    </>
  );
}

export function VehicleLocationTab({ vehicleId }: { vehicleId: string }) {
  return <Suspense fallback={<Skeleton height={420} />}><VehicleLocation vehicleId={vehicleId} /></Suspense>;
}

// ---------------- Settings: providers and devices

export function TrackingSettingsPage() {
  const providers = trackingApi.useProviders();
  const devices = trackingApi.useDevices();
  const removeProvider = trackingApi.useRemoveProvider();
  const removeDevice = trackingApi.useRemoveDevice();
  const rotate = trackingApi.useRotateKey();
  const [editingProvider, setEditingProvider] = useState<TrackingProvider | 'new' | null>(null);
  const [editingDevice, setEditingDevice] = useState<TrackingDevice | 'new' | null>(null);
  const [linking, setLinking] = useState<TrackingDevice | null>(null);
  const [shownKey, setShownKey] = useState<{ device: TrackingDevice; apiKey: string } | null>(null);

  if ((providers.error && !providers.data) || (devices.error && !devices.data))
    return <><PageHeader title="Rastreadores" /><Paper><ErrorState error={providers.error ?? devices.error} onRetry={() => { void providers.refetch(); void devices.refetch(); }} /></Paper></>;

  const providerList = providers.data ?? [];
  const deviceList = devices.data ?? [];
  const newDevice = (
    <Button leftSection={<IconPlus size={18} />} onClick={() => setEditingDevice('new')} disabled={providerList.length === 0}>Novo rastreador</Button>
  );

  return (
    <>
      <PageHeader title="Rastreadores" description="Aparelhos de GPS que enviam a posição dos veículos. Cada rastreador tem uma chave própria para enviar dados ao sistema."
        breadcrumbs={[{ label: 'Mapa da frota', to: '/mapa' }, { label: 'Rastreadores' }]} action={newDevice} />
      <Stack gap="lg">
        <Paper p="md">
          <Group justify="space-between" mb="sm">
            <div><Title order={4} fz="md">Provedores</Title><Text size="sm" c="dimmed">Empresa ou plataforma de rastreamento.</Text></div>
            <Button variant="default" size="xs" leftSection={<IconPlus size={14} />} onClick={() => setEditingProvider('new')}>Novo provedor</Button>
          </Group>
          {!providers.data ? <Skeleton height={60} /> : providerList.length === 0 ? (
            <EmptyState icon={<IconPlugConnected size={28} />} title="Nenhum provedor" description="Cadastre o provedor de rastreamento (ex.: a empresa que fornece os aparelhos) para depois cadastrar os rastreadores." />
          ) : (
            <Table.ScrollContainer minWidth={480}>
              <Table>
                <Table.Thead><Table.Tr><Table.Th>Nome</Table.Th><Table.Th>Tipo</Table.Th><Table.Th>Rastreadores</Table.Th><Table.Th>Situação</Table.Th><Table.Th w={56} /></Table.Tr></Table.Thead>
                <Table.Tbody>
                  {providerList.map((p) => (
                    <Table.Tr key={p.id}>
                      <Table.Td>{p.name}</Table.Td><Table.Td>{PROVIDER_KIND[p.kind].label}</Table.Td><Table.Td>{p.devices}</Table.Td>
                      <Table.Td>{p.isActive ? 'Ativo' : 'Inativo'}</Table.Td>
                      <Table.Td><RowActions onEdit={() => setEditingProvider(p)} onDelete={() => confirmDelete({
                        entity: 'o provedor', name: p.name, onConfirm: () => removeProvider.mutate(p.id, {
                          onSuccess: () => notifySuccess('Provedor excluído.'), onError: (e) => notifyError(e, 'O provedor não foi excluído'),
                        }),
                      })} /></Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
        </Paper>

        <Paper p="md">
          <Title order={4} fz="md" mb="sm">Rastreadores</Title>
          {!devices.data ? <Skeleton height={80} /> : deviceList.length === 0 ? (
            <EmptyState icon={<IconRouter size={28} />} title="Nenhum rastreador" description="Cadastre um rastreador, copie a chave gerada para a configuração do aparelho e instale-o em um veículo." action={newDevice} />
          ) : (
            <Table.ScrollContainer minWidth={760}>
              <Table highlightOnHover>
                <Table.Thead><Table.Tr><Table.Th>Identificador</Table.Th><Table.Th>Provedor</Table.Th><Table.Th>Veículo</Table.Th><Table.Th>Chave</Table.Th><Table.Th>Sinal</Table.Th><Table.Th w={56} /></Table.Tr></Table.Thead>
                <Table.Tbody>
                  {deviceList.map((d) => (
                    <Table.Tr key={d.id} style={{ opacity: d.isActive ? 1 : 0.6 }}>
                      <Table.Td><Text size="sm" fw={600}>{d.identifier}</Text>{d.model && <Text size="xs" c="dimmed">{d.model}</Text>}</Table.Td>
                      <Table.Td>{d.providerName}</Table.Td>
                      <Table.Td>{d.vehicleId ? <Link to={`/veiculos/${d.vehicleId}?aba=localizacao`}>{formatPlate(d.licensePlate!)}</Link> : <Text size="sm" c="dimmed">não instalado</Text>}</Table.Td>
                      <Table.Td><Code>{d.apiKeyPrefix}…</Code></Table.Td>
                      <Table.Td>
                        <Badge color={CONNECTION_STATUS[d.status].color} variant="light">{CONNECTION_STATUS[d.status].label}</Badge>
                        {d.lastPositionAt && <Text size="xs" c="dimmed">{formatDateTime(d.lastPositionAt)}</Text>}
                      </Table.Td>
                      <Table.Td>
                        <RowActions onEdit={() => setEditingDevice(d)}
                          onDelete={() => confirmDelete({ entity: 'o rastreador', name: d.identifier, onConfirm: () => removeDevice.mutate(d.id, {
                            onSuccess: () => notifySuccess('Rastreador excluído. A chave dele deixou de funcionar.'), onError: (e) => notifyError(e, 'O rastreador não foi excluído'),
                          }) })}
                          extra={<>
                            <MenuItem label={d.vehicleId ? 'Trocar ou remover do veículo' : 'Instalar em um veículo'} onClick={() => setLinking(d)} />
                            <MenuItem label="Gerar nova chave" onClick={() => rotate.mutate(d.id, {
                              onSuccess: (r) => setShownKey(r), onError: (e) => notifyError(e, 'A chave não foi gerada'),
                            })} />
                          </>} />
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
        </Paper>
      </Stack>
      {editingProvider && <ProviderModal provider={editingProvider === 'new' ? null : editingProvider} onClose={() => setEditingProvider(null)} />}
      {editingDevice && <DeviceModal device={editingDevice === 'new' ? null : editingDevice} providers={providerList}
        onClose={() => setEditingDevice(null)} onCreated={(r) => { setEditingDevice(null); setShownKey(r); }} />}
      {linking && <LinkModal device={linking} onClose={() => setLinking(null)} />}
      {shownKey && <KeyModal result={shownKey} onClose={() => setShownKey(null)} />}
    </>
  );
}

function MenuItem({ label, onClick }: { label: string; onClick: () => void }) {
  return <Menu.Item leftSection={<IconKey size={16} />} onClick={onClick}>{label}</Menu.Item>;
}

function ProviderModal({ provider, onClose }: { provider: TrackingProvider | null; onClose: () => void }) {
  const mutation = trackingApi.useSaveProvider(provider?.id);
  const form = useForm<{ name: string; kind: ProviderKind; notes: string; isActive: boolean }>({
    initialValues: { name: provider?.name ?? '', kind: provider?.kind ?? 'GenericHttp', notes: provider?.notes ?? '', isActive: provider?.isActive ?? true },
    validate: { name: (v: string) => (v.trim() ? null : 'Nome: campo obrigatório.') },
  });
  return (
    <Modal opened onClose={onClose} title={provider ? 'Editar provedor' : 'Novo provedor'} centered>
      <form onSubmit={form.onSubmit((v) => mutation.mutate({ ...v, name: v.name.trim(), notes: v.notes.trim() || null }, {
        onSuccess: () => { notifySuccess('Provedor salvo com sucesso.'); onClose(); },
        onError: (e) => form.setErrors(notifyError(e, 'O provedor não foi salvo').fieldErrors),
      }))}>
        <Stack>
          <TextInput label="Nome" withAsterisk maxLength={100} {...form.getInputProps('name')} />
          <Select label="Tipo" data={toSelectData(PROVIDER_KIND)} allowDeselect={false} {...form.getInputProps('kind')} />
          <Textarea label="Observações" autosize minRows={2} maxLength={500} {...form.getInputProps('notes')} />
          <Switch label="Ativo" {...form.getInputProps('isActive', { type: 'checkbox' })} description="Provedor inativo: seus rastreadores param de ser aceitos." />
          <Group justify="flex-end"><Button variant="default" onClick={onClose}>Cancelar</Button><Button type="submit" loading={mutation.isPending}>Salvar</Button></Group>
        </Stack>
      </form>
    </Modal>
  );
}

function DeviceModal({ device, providers, onClose, onCreated }: {
  device: TrackingDevice | null; providers: TrackingProvider[]; onClose: () => void; onCreated: (r: { device: TrackingDevice; apiKey: string }) => void;
}) {
  const create = trackingApi.useCreateDevice();
  const update = trackingApi.useUpdateDevice(device?.id ?? '');
  const form = useForm<{ trackingProviderId: string; identifier: string; model: string; isActive: boolean }>({
    initialValues: { trackingProviderId: device?.trackingProviderId ?? providers[0]?.id ?? '', identifier: device?.identifier ?? '', model: device?.model ?? '', isActive: device?.isActive ?? true },
    validate: {
      trackingProviderId: (v: string) => (v ? null : 'Provedor: campo obrigatório.'),
      identifier: (v: string) => (v.trim() ? null : 'Identificador: campo obrigatório.'),
    },
  });
  const pending = create.isPending || update.isPending;
  return (
    <Modal opened onClose={onClose} title={device ? 'Editar rastreador' : 'Novo rastreador'} centered>
      <form onSubmit={form.onSubmit((v) => {
        const body = { ...v, identifier: v.identifier.trim(), model: v.model.trim() || null };
        const onError = (e: unknown) => form.setErrors(notifyError(e, 'O rastreador não foi salvo').fieldErrors);
        if (device) update.mutate(body, { onSuccess: () => { notifySuccess('Rastreador salvo com sucesso.'); onClose(); }, onError });
        else create.mutate(body, { onSuccess: onCreated, onError });
      })}>
        <Stack>
          <Select label="Provedor" withAsterisk data={providers.map((p) => ({ value: p.id, label: p.name }))} allowDeselect={false} {...form.getInputProps('trackingProviderId')} />
          <TextInput label="Identificador" description="IMEI, número de série ou código do aparelho no provedor." withAsterisk maxLength={50} {...form.getInputProps('identifier')} />
          <TextInput label="Modelo" maxLength={100} {...form.getInputProps('model')} />
          <Switch label="Ativo" {...form.getInputProps('isActive', { type: 'checkbox' })} description="Rastreador inativo: as posições enviadas são recusadas." />
          {!device && <Alert variant="light" color="blue" p="xs"><Text size="xs">Ao salvar, o sistema gera a chave do rastreador e mostra uma única vez.</Text></Alert>}
          <Group justify="flex-end"><Button variant="default" onClick={onClose}>Cancelar</Button><Button type="submit" loading={pending}>Salvar</Button></Group>
        </Stack>
      </form>
    </Modal>
  );
}

function LinkModal({ device, onClose }: { device: TrackingDevice; onClose: () => void }) {
  const link = trackingApi.useLink();
  const [vehicleId, setVehicleId] = useState<string | null>(device.vehicleId);
  const save = (target: string | null) => link.mutate({ id: device.id, vehicleId: target }, {
    onSuccess: () => { notifySuccess(target ? 'Rastreador instalado no veículo.' : 'Rastreador removido do veículo.'); onClose(); },
    onError: (e) => notifyError(e, 'A instalação não foi salva'),
  });
  return (
    <Modal opened onClose={onClose} title={`Instalação do rastreador ${device.identifier}`} centered>
      <Stack>
        <VehiclePicker label="Veículo" value={vehicleId} onChange={setVehicleId} />
        <Text size="xs" c="dimmed">Se o veículo já tiver outro rastreador, ele será desinstalado. O histórico de posições é mantido.</Text>
        <Group justify="space-between">
          {device.vehicleId ? <Button variant="subtle" color="red" onClick={() => save(null)} loading={link.isPending}>Remover do veículo</Button> : <span />}
          <Group><Button variant="default" onClick={onClose}>Cancelar</Button><Button onClick={() => save(vehicleId)} disabled={!vehicleId} loading={link.isPending}>Salvar</Button></Group>
        </Group>
      </Stack>
    </Modal>
  );
}

function KeyModal({ result, onClose }: { result: { device: TrackingDevice; apiKey: string }; onClose: () => void }) {
  const url = `${window.location.origin}/api/v1/tracking/ingest`;
  const example = `POST ${url}
X-Device-Key: ${result.apiKey}
Content-Type: application/json

{ "positions": [ { "recordedAt": "2026-10-05T12:00:00Z", "latitude": -25.4284, "longitude": -49.2733,
                   "speedKmh": 72, "heading": 90, "ignition": true, "odometerKm": 120500 } ] }`;
  return (
    <Modal opened onClose={onClose} title={`Chave do rastreador ${result.device.identifier}`} centered size="lg" closeOnClickOutside={false}>
      <Stack>
        <Alert color="orange" variant="light">Copie agora: por segurança, esta chave não será mostrada de novo. Se perder, gere uma nova (a antiga deixa de funcionar).</Alert>
        <Group gap="xs" wrap="nowrap">
          <Code block style={{ flex: 1, wordBreak: 'break-all' }}>{result.apiKey}</Code>
          <CopyButton value={result.apiKey}>
            {({ copied, copy }) => <Button size="xs" variant="default" onClick={copy} leftSection={copied ? <IconCheck size={14} /> : <IconCopy size={14} />}>{copied ? 'Copiada' : 'Copiar'}</Button>}
          </CopyButton>
        </Group>
        <Text size="sm" fw={600}>Como o rastreador (ou a plataforma do provedor) envia as posições</Text>
        <Code block>{example}</Code>
        <Text size="xs" c="dimmed">Até 500 posições por envio. Posições repetidas são ignoradas; posições inválidas (coordenadas fora do mapa, 0,0, data no futuro ou com mais de 30 dias) são recusadas com o motivo.</Text>
        <Group justify="flex-end"><Button onClick={onClose}>Já copiei a chave</Button></Group>
      </Stack>
    </Modal>
  );
}

// ---------------- Integrations

export function IntegrationsPage() {
  const query = trackingApi.useIntegrations();
  return (
    <>
      <PageHeader title="Integrações" description="Com o que o sistema se conecta e como cada integração está configurada." />
      {query.error && !query.data ? <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper> : !query.data ? <Skeleton height={300} /> : (
        <SimpleGrid cols={{ base: 1, md: 2 }} spacing="md">
          {query.data.map((i) => (
            <Paper key={i.key} p="md">
              <Group justify="space-between" mb={4} wrap="nowrap">
                <Text fw={650}>{i.name}</Text>
                <Badge color={INTEGRATION_STATE[i.state].color} variant="light">{INTEGRATION_STATE[i.state].label}</Badge>
              </Group>
              <Text size="sm" c="dimmed">{i.description}</Text>
              <Text size="sm" mt="xs">{i.detail}</Text>
              {i.link && <Button component={Link} to={i.link} variant="subtle" size="compact-sm" mt="xs" rightSection={<IconArrowRight size={14} />}>Abrir</Button>}
            </Paper>
          ))}
        </SimpleGrid>
      )}
    </>
  );
}
