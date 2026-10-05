import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { ListParams, PagedResult } from '../../api/crud';
import type { AlertCategory } from '../alerts/alerts';

export interface VehicleMetrics {
  vehicleId: string;
  licensePlate: string;
  model: string;
  type: string;
  status: string;
  kmDriven: number | null;
  kmReliable: boolean;
  fuelQuantity: number | null;
  averageConsumption: number | null;
  fuelCost: number | null;
  maintenanceCost: number | null;
  tireCost: number | null;
  otherCost: number | null;
  totalCost: number | null;
  costPerKm: number | null;
  workOrdersCompleted: number | null;
  correctiveWorkOrders: number | null;
  downtimeHours: number | null;
  tireReplacements: number | null;
  isPartial: boolean;
}

export interface RecurringProblem {
  vehicleId: string;
  licensePlate: string;
  problem: string;
  occurrences: number;
  lastCompletedAt: string;
  workOrderNumbers: number[];
}

export interface Benchmark {
  label: string;
  vehicleCount: number;
  average: VehicleMetrics;
}

export interface VehicleComparison {
  from: string;
  to: string;
  vehicles: VehicleMetrics[];
  benchmarks: Benchmark[];
}

export type HealthStatus = 'Good' | 'Attention' | 'Critical' | 'NotVisible';
export type HealthArea = 'Maintenance' | 'Fuel' | 'Tires' | 'Costs' | 'Documents' | 'Occurrences' | 'Mileage';
export type HealthLevel = 'Good' | 'Attention' | 'Critical';

export interface VehicleHealth {
  vehicleId: string;
  score: number;
  level: HealthLevel;
  factors: { area: HealthArea; status: HealthStatus; explanation: string; tab: string | null }[];
  isPartial: boolean;
}

export interface Insight {
  key: string;
  category: AlertCategory;
  sentiment: 'Neutral' | 'Positive' | 'Negative';
  title: string;
  explanation: string;
  evidence: string;
  link: string | null;
  magnitude: number;
  direction: 'Stable' | 'Up' | 'Down';
}

export type SearchResultType =
  | 'Vehicle' | 'Implement' | 'Driver' | 'Tire' | 'WorkOrder' | 'Fueling' | 'Expense' | 'Document' | 'Occurrence' | 'Alert';

export interface SearchResult {
  type: SearchResultType;
  title: string;
  subtitle: string | null;
  link: string;
}

export const SEARCH_TYPE_LABEL: Record<SearchResultType, string> = {
  Vehicle: 'Veículos',
  Implement: 'Implementos',
  Driver: 'Motoristas',
  Tire: 'Pneus',
  WorkOrder: 'Ordens de serviço',
  Fueling: 'Abastecimentos',
  Expense: 'Despesas',
  Document: 'Documentos',
  Occurrence: 'Ocorrências',
  Alert: 'Alertas',
};

export const HEALTH_AREA_LABEL: Record<HealthArea, string> = {
  Maintenance: 'Manutenção',
  Fuel: 'Combustível',
  Tires: 'Pneus',
  Costs: 'Custos',
  Documents: 'Documentos',
  Occurrences: 'Ocorrências',
  Mileage: 'Hodômetro',
};

export const HEALTH_STATUS: Record<HealthStatus, { label: string; color: string }> = {
  Good: { label: 'Bom', color: 'teal' },
  Attention: { label: 'Atenção', color: 'orange' },
  Critical: { label: 'Crítico', color: 'red' },
  NotVisible: { label: 'Sem acesso', color: 'gray' },
};

export const HEALTH_LEVEL: Record<HealthLevel, { label: string; color: string }> = {
  Good: { label: 'Boa', color: 'teal' },
  Attention: { label: 'Atenção', color: 'orange' },
  Critical: { label: 'Crítica', color: 'red' },
};

const clean = (params: ListParams) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));

export const analyticsApi = {
  useFleetPerformance: (params: ListParams) =>
    useQuery({
      queryKey: ['analytics', 'fleet-performance', params],
      queryFn: () => api.get<PagedResult<VehicleMetrics>>('/analytics/fleet-performance', { params: clean(params) }).then((r) => r.data),
      placeholderData: keepPreviousData,
    }),
  useRecurringProblems: (from: string, to: string) =>
    useQuery({
      queryKey: ['analytics', 'recurring', from, to],
      queryFn: () => api.get<RecurringProblem[]>('/analytics/recurring-problems', { params: { from, to } }).then((r) => r.data),
    }),
  useCompare: (ids: string[], from: string, to: string) =>
    useQuery({
      queryKey: ['analytics', 'compare', ids, from, to],
      // Repeated "vehicleIds" keys (ASP.NET array binding).
      queryFn: () => api.get<VehicleComparison>('/analytics/compare', {
        params: new URLSearchParams([...ids.map((id) => ['vehicleIds', id]), ['from', from], ['to', to]]),
      }).then((r) => r.data),
      enabled: ids.length > 0,
      placeholderData: keepPreviousData,
    }),
  useHealth: (vehicleId: string | undefined) =>
    useQuery({ queryKey: ['analytics', 'health', vehicleId], queryFn: () => api.get<VehicleHealth>(`/vehicles/${vehicleId}/health`).then((r) => r.data), enabled: !!vehicleId }),
  useInsights: () => useQuery({ queryKey: ['analytics', 'insights'], queryFn: () => api.get<Insight[]>('/analytics/insights').then((r) => r.data) }),
  useSearch: (q: string) =>
    useQuery({
      queryKey: ['search', q],
      queryFn: () => api.get<{ query: string; results: SearchResult[] }>('/search', { params: { q } }).then((r) => r.data),
      enabled: q.trim().length >= 2,
      placeholderData: keepPreviousData,
    }),
};
