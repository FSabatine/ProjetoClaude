import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';

export type ConnectionStatus = 'NeverConnected' | 'Online' | 'Recent' | 'Offline';
export type ProviderKind = 'GenericHttp' | 'Simulator';

export interface VehicleLocation {
  vehicleId: string;
  licensePlate: string;
  model: string;
  operationalStatus: string;
  driverName: string | null;
  latitude: number | null;
  longitude: number | null;
  speedKmh: number | null;
  heading: number | null;
  ignition: boolean | null;
  recordedAt: string | null;
  status: ConnectionStatus;
  deviceIdentifier: string | null;
}

export interface TrackPoint {
  recordedAt: string;
  latitude: number;
  longitude: number;
  speedKmh: number | null;
  ignition: boolean | null;
}

export interface VehicleTrack {
  vehicleId: string;
  from: string;
  to: string;
  points: TrackPoint[];
  truncated: boolean;
  maxSpeedKmh: number | null;
  count: number;
}

export interface TrackingProvider {
  id: string;
  name: string;
  kind: ProviderKind;
  notes: string | null;
  isActive: boolean;
  devices: number;
}

export interface TrackingDevice {
  id: string;
  trackingProviderId: string;
  providerName: string;
  identifier: string;
  model: string | null;
  isActive: boolean;
  apiKeyPrefix: string;
  keyIssuedAt: string | null;
  vehicleId: string | null;
  licensePlate: string | null;
  linkedSince: string | null;
  lastPositionAt: string | null;
  status: ConnectionStatus;
}

export interface IntegrationStatus {
  key: string;
  name: string;
  description: string;
  state: 'Active' | 'Available' | 'NotConfigured' | 'Planned';
  detail: string;
  link: string | null;
}

export const CONNECTION_STATUS: Record<ConnectionStatus, { label: string; color: string; hex: string }> = {
  Online: { label: 'Transmitindo', color: 'teal', hex: '#12b886' },
  Recent: { label: 'Visto nas últimas 24 h', color: 'orange', hex: '#fd7e14' },
  Offline: { label: 'Sem sinal há mais de 24 h', color: 'gray', hex: '#868e96' },
  NeverConnected: { label: 'Nunca transmitiu', color: 'gray', hex: '#adb5bd' },
};

export const PROVIDER_KIND: Record<ProviderKind, { label: string }> = {
  GenericHttp: { label: 'Envio por API (genérico)' },
  Simulator: { label: 'Simulador / testes' },
};

export const INTEGRATION_STATE: Record<IntegrationStatus['state'], { label: string; color: string }> = {
  Active: { label: 'Em uso', color: 'teal' },
  Available: { label: 'Disponível', color: 'blue' },
  NotConfigured: { label: 'Não configurado', color: 'gray' },
  Planned: { label: 'Previsto', color: 'grape' },
};

/** Tiles from OpenStreetMap by default (attribution required); production with heavy use should set its own provider. */
export const TILE_URL = import.meta.env.VITE_MAP_TILE_URL ?? 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
export const TILE_ATTRIBUTION = import.meta.env.VITE_MAP_TILE_ATTRIBUTION ?? '&copy; colaboradores do <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';

function useInvalidating<TBody, TResult>(fn: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['tracking'] }) });
}

export const trackingApi = {
  useFleet: () => useQuery({ queryKey: ['tracking', 'fleet'], queryFn: () => api.get<VehicleLocation[]>('/tracking/fleet').then((r) => r.data), refetchInterval: 60_000 }),
  useTrack: (vehicleId: string, hours: number) =>
    useQuery({
      queryKey: ['tracking', 'track', vehicleId, hours],
      queryFn: () => api.get<VehicleTrack>(`/vehicles/${vehicleId}/track`, { params: { from: new Date(Date.now() - hours * 3_600_000).toISOString() } }).then((r) => r.data),
    }),
  useProviders: () => useQuery({ queryKey: ['tracking', 'providers'], queryFn: () => api.get<TrackingProvider[]>('/tracking/providers').then((r) => r.data) }),
  useDevices: () => useQuery({ queryKey: ['tracking', 'devices'], queryFn: () => api.get<TrackingDevice[]>('/tracking/devices').then((r) => r.data) }),
  useSaveProvider: (id?: string) =>
    useInvalidating((body: { name: string; kind: ProviderKind; notes: string | null; isActive: boolean }) =>
      (id ? api.put<TrackingProvider>(`/tracking/providers/${id}`, body) : api.post<TrackingProvider>('/tracking/providers', body)).then((r) => r.data)),
  useRemoveProvider: () => useInvalidating((id: string) => api.delete(`/tracking/providers/${id}`).then(() => undefined)),
  useCreateDevice: () =>
    useInvalidating((body: { trackingProviderId: string; identifier: string; model: string | null; isActive: boolean }) =>
      api.post<{ device: TrackingDevice; apiKey: string }>('/tracking/devices', body).then((r) => r.data)),
  useUpdateDevice: (id: string) =>
    useInvalidating((body: { trackingProviderId: string; identifier: string; model: string | null; isActive: boolean }) =>
      api.put<TrackingDevice>(`/tracking/devices/${id}`, body).then((r) => r.data)),
  useRotateKey: () => useInvalidating((id: string) => api.post<{ device: TrackingDevice; apiKey: string }>(`/tracking/devices/${id}/rotate-key`).then((r) => r.data)),
  useLink: () =>
    useInvalidating((body: { id: string; vehicleId: string | null }) =>
      api.post<TrackingDevice>(`/tracking/devices/${body.id}/vehicle`, { vehicleId: body.vehicleId }).then((r) => r.data)),
  useRemoveDevice: () => useInvalidating((id: string) => api.delete(`/tracking/devices/${id}`).then(() => undefined)),
  useIntegrations: () => useQuery({ queryKey: ['tracking', 'integrations'], queryFn: () => api.get<IntegrationStatus[]>('/integrations').then((r) => r.data) }),
};
