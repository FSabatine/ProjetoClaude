import 'leaflet/dist/leaflet.css';
import { useEffect, useMemo, useState } from 'react';
import { Anchor, Badge, Group, Paper, ScrollArea, SegmentedControl, SimpleGrid, Skeleton, Stack, Text, UnstyledButton } from '@mantine/core';
import { IconMapPinOff } from '@tabler/icons-react';
import { latLngBounds, type LatLngBoundsExpression, type LatLngExpression } from 'leaflet';
import { CircleMarker, MapContainer, Polyline, Popup, TileLayer, Tooltip, useMap } from 'react-leaflet';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { StatusBadge } from '../../components/common';
import { EmptyState, ErrorState } from '../../components/States';
import { formatDateTime, formatNumber, formatPlate } from '../../lib/format';
import { VEHICLE_OPERATIONAL_STATUS } from '../operations/labels';
import { CONNECTION_STATUS, TILE_ATTRIBUTION, TILE_URL, trackingApi, type ConnectionStatus, type VehicleLocation } from './tracking';

/** Southern Brazil — only used before there is any position to frame. */
const DEFAULT_CENTER: LatLngExpression = [-25.43, -49.27];

function FitBounds({ bounds }: { bounds: LatLngBoundsExpression | null }) {
  const map = useMap();
  useEffect(() => {
    if (bounds) map.fitBounds(bounds, { padding: [40, 40], maxZoom: 13 });
  }, [map, bounds]);
  return null;
}

function FlyTo({ target }: { target: LatLngExpression | null }) {
  const map = useMap();
  useEffect(() => {
    if (target) map.flyTo(target, Math.max(map.getZoom(), 12), { duration: 0.6 });
  }, [map, target]);
  return null;
}

const ago = (iso: string | null) => (iso ? formatDateTime(iso) : 'sem posição');

/** Fleet map (spec §21): last known position of each tracked vehicle, colored by connection status (with text in the list). */
export function FleetMap() {
  const { can } = useAuth();
  const query = trackingApi.useFleet();
  const [filter, setFilter] = useState<'all' | ConnectionStatus>('all');
  const [selected, setSelected] = useState<string | null>(null);
  const data = query.data;
  const located = useMemo(() => (data ?? []).filter((v) => v.latitude !== null && v.longitude !== null), [data]);
  const shown = (data ?? []).filter((v) => filter === 'all' || v.status === filter);
  const bounds = useMemo(() => (located.length ? latLngBounds(located.map((v) => [v.latitude!, v.longitude!] as [number, number])) : null), [located]);
  const target = selected ? located.find((v) => v.vehicleId === selected) : null;

  if (query.error && !data) return <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper>;
  if (!data) return <Skeleton height={520} />;
  if (data.length === 0) {
    return (
      <Paper p="lg">
        <EmptyState icon={<IconMapPinOff size={28} />} title="Nenhum veículo rastreado"
          description="Cadastre um rastreador, instale-o em um veículo e configure o aparelho para enviar as posições. Os veículos aparecem aqui assim que a primeira posição chegar."
          action={can(PERMISSIONS.tracking.manage) ? <Anchor component={Link} to="/configuracoes/rastreadores">Configurar rastreadores</Anchor> : undefined} />
      </Paper>
    );
  }

  return (
    <SimpleGrid cols={{ base: 1, lg: 3 }} spacing="md">
      <Paper style={{ gridColumn: 'span 2', overflow: 'hidden', minHeight: 520 }}>
        <MapContainer center={DEFAULT_CENTER} zoom={8} style={{ height: 560, width: '100%' }} scrollWheelZoom>
          <TileLayer url={TILE_URL} attribution={TILE_ATTRIBUTION} />
          <FitBounds bounds={bounds} />
          <FlyTo target={target ? [target.latitude!, target.longitude!] : null} />
          {located.filter((v) => filter === 'all' || v.status === filter).map((v) => (
            <CircleMarker key={v.vehicleId} center={[v.latitude!, v.longitude!]} radius={v.vehicleId === selected ? 11 : 8}
              pathOptions={{ color: '#fff', weight: 2, fillColor: CONNECTION_STATUS[v.status].hex, fillOpacity: 0.95 }}
              eventHandlers={{ click: () => setSelected(v.vehicleId) }}>
              <Tooltip direction="top" offset={[0, -8]}>{formatPlate(v.licensePlate)}</Tooltip>
              <Popup><LocationDetails v={v} /></Popup>
            </CircleMarker>
          ))}
        </MapContainer>
      </Paper>
      <Paper p="sm">
        <SegmentedControl fullWidth size="xs" mb="sm" value={filter} onChange={(v) => setFilter(v as typeof filter)}
          data={[{ value: 'all', label: 'Todos' }, { value: 'Online', label: 'Transmitindo' }, { value: 'Recent', label: '24 h' }, { value: 'Offline', label: 'Sem sinal' }]} />
        <ScrollArea.Autosize mah={500}>
          <Stack gap={4}>
            {shown.length === 0 && <Text size="sm" c="dimmed" p="sm">Nenhum veículo nesta situação.</Text>}
            {shown.map((v) => (
              <UnstyledButton key={v.vehicleId} onClick={() => setSelected(v.vehicleId)} p="xs"
                style={{ borderRadius: 8, background: v.vehicleId === selected ? 'var(--mantine-color-default-hover)' : undefined }}>
                <Group justify="space-between" wrap="nowrap">
                  <div style={{ minWidth: 0 }}>
                    <Text fw={700} ff="monospace" size="sm">{formatPlate(v.licensePlate)}</Text>
                    <Text size="xs" c="dimmed" truncate>{v.model} · {ago(v.recordedAt)}</Text>
                  </div>
                  <Badge color={CONNECTION_STATUS[v.status].color} variant="light">{CONNECTION_STATUS[v.status].label}</Badge>
                </Group>
              </UnstyledButton>
            ))}
          </Stack>
        </ScrollArea.Autosize>
      </Paper>
    </SimpleGrid>
  );
}

function LocationDetails({ v }: { v: VehicleLocation }) {
  return (
    <Stack gap={2} miw={200}>
      <Anchor component={Link} to={`/veiculos/${v.vehicleId}?aba=localizacao`} fw={700} ff="monospace">{formatPlate(v.licensePlate)}</Anchor>
      <Text size="xs">{v.model}</Text>
      <Group gap={4}>
        <StatusBadge value={v.operationalStatus as keyof typeof VEHICLE_OPERATIONAL_STATUS} map={VEHICLE_OPERATIONAL_STATUS} />
        <Badge color={CONNECTION_STATUS[v.status].color} variant="light">{CONNECTION_STATUS[v.status].label}</Badge>
      </Group>
      {v.driverName && <Text size="xs">Motorista: {v.driverName}</Text>}
      <Text size="xs">Velocidade: {v.speedKmh === null ? '—' : `${formatNumber(v.speedKmh)} km/h`} · Ignição: {v.ignition === null ? '—' : v.ignition ? 'ligada' : 'desligada'}</Text>
      <Text size="xs" c="dimmed">Última posição: {ago(v.recordedAt)}</Text>
    </Stack>
  );
}

const PERIODS = [{ value: '24', label: '24 horas' }, { value: '72', label: '3 dias' }, { value: '168', label: '7 dias' }];

/** "Localização" tab of the vehicle hub: route of the period and the last known position. */
export function VehicleLocationPanel({ vehicleId }: { vehicleId: string }) {
  const [hours, setHours] = useState('24');
  const query = trackingApi.useTrack(vehicleId, Number(hours));
  const track = query.data;
  const line = useMemo(() => (track?.points ?? []).map((p) => [p.latitude, p.longitude] as [number, number]), [track]);
  const bounds = useMemo(() => (line.length ? latLngBounds(line) : null), [line]);
  const last = track ? track.points[track.points.length - 1] : undefined;

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <SegmentedControl value={hours} onChange={setHours} data={PERIODS} />
        <Anchor component={Link} to="/mapa" size="sm">Ver no mapa da frota</Anchor>
      </Group>
      {query.error && !track && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
      {!track && !query.error && <Skeleton height={420} />}
      {track && track.count === 0 && (
        <Paper p="lg"><EmptyState icon={<IconMapPinOff size={28} />} title="Sem posições no período"
          description="Este veículo não enviou posições no período escolhido. Confira se há um rastreador instalado nele em Configurações › Rastreadores." /></Paper>
      )}
      {track && track.count > 0 && (
        <>
          <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="sm">
            <Paper p="sm"><Text size="xs" c="dimmed">Última posição</Text><Text fw={600} size="sm">{formatDateTime(last!.recordedAt)}</Text></Paper>
            <Paper p="sm"><Text size="xs" c="dimmed">Pontos no período</Text><Text fw={600} size="sm">{formatNumber(track.count)}{track.truncated ? ' (mapa simplificado)' : ''}</Text></Paper>
            <Paper p="sm"><Text size="xs" c="dimmed">Velocidade máxima</Text><Text fw={600} size="sm">{track.maxSpeedKmh === null ? '—' : `${formatNumber(track.maxSpeedKmh)} km/h`}</Text></Paper>
            <Paper p="sm"><Text size="xs" c="dimmed">Ignição agora</Text><Text fw={600} size="sm">{last!.ignition === null ? '—' : last!.ignition ? 'Ligada' : 'Desligada'}</Text></Paper>
          </SimpleGrid>
          <Paper style={{ overflow: 'hidden' }}>
            <MapContainer center={line[line.length - 1]} zoom={11} style={{ height: 420, width: '100%' }} scrollWheelZoom={false}>
              <TileLayer url={TILE_URL} attribution={TILE_ATTRIBUTION} />
              <FitBounds bounds={bounds} />
              <Polyline positions={line} pathOptions={{ color: '#1c7ed6', weight: 4, opacity: 0.8 }} />
              <CircleMarker center={line[0]} radius={6} pathOptions={{ color: '#fff', weight: 2, fillColor: '#868e96', fillOpacity: 1 }}>
                <Tooltip>Início do período</Tooltip>
              </CircleMarker>
              <CircleMarker center={line[line.length - 1]} radius={9} pathOptions={{ color: '#fff', weight: 2, fillColor: '#12b886', fillOpacity: 1 }}>
                <Tooltip permanent direction="top" offset={[0, -8]}>Agora</Tooltip>
              </CircleMarker>
            </MapContainer>
          </Paper>
          <Text size="xs" c="dimmed">Mapa © OpenStreetMap. A rota liga as posições enviadas pelo rastreador; não é um traçado de estrada.</Text>
        </>
      )}
    </Stack>
  );
}
