import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { ListParams, PagedResult } from '../../api/crud';
import type { HistoryEntry } from '../operations/api';
import type {
  AssetTires,
  CostReportRow,
  InspectionReportRow,
  LifecycleReportRow,
  Tire,
  TireCompatibility,
  TireCost,
  TireDashboard,
  TireInspection,
  TireInstallation,
  TireLayout,
  TireListItem,
  TireModel,
  TireReportPage,
  TireRequest,
  TireServiceOrder,
  TireSettings,
} from './tires';

const clean = (params: ListParams) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));

const get = <T>(url: string, params?: ListParams) => api.get<T>(url, { params: params && clean(params) }).then((r) => r.data);

/** A tire operation changes the tire, the vehicle layout, dashboards and timelines at once: refresh everything (same as Phases 2–4). */
function useOperation<TBody, TResult>(fn: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries() });
}

// Request bodies (mirror the API records; optional fields are simply omitted).
export type InstallBody = { vehicleId?: string | null; implementId?: string | null; positionCode: string; performedAt?: string | null; odometerKm?: number | null; notes?: string | null };
export type RemovalBody = {
  performedAt?: string | null; odometerKm?: number | null; reason: string; destination: string; treadDepthMm?: number | null; pressure?: number | null;
  pressureUnit?: string | null; condition?: string | null; wearPattern?: string | null; damages?: string[]; notes?: string | null; storageLocation?: string | null;
  workshopId?: string | null; providerName?: string | null; repairType?: string | null; disposalReason?: string | null; disposalDestination?: string | null;
  fileIds?: string[];
};
export type ReplaceBody = RemovalBody & { replacementTireId: string; installNotes?: string | null };
export type TransferBody = { vehicleId?: string | null; implementId?: string | null; positionCode: string; performedAt?: string | null; originOdometerKm?: number | null; odometerKm?: number | null; notes?: string | null };
export type RotationBody = { vehicleId?: string | null; implementId?: string | null; performedAt?: string | null; odometerKm?: number | null; reason?: string | null; notes?: string | null; moves: { tireId: string; toPositionCode: string }[] };
export type InspectionBody = {
  inspectedAt?: string | null; odometerKm?: number | null; treadDepthMm?: number | null; pressure?: number | null; pressureUnit?: string | null;
  condition: string; wearPattern: string; damages: string[]; notes?: string | null; fileIds?: string[]; occurrenceId?: string | null;
};
export type ServiceBody = { kind: string; sentAt?: string | null; workshopId?: string | null; providerName?: string | null; repairType?: string | null; description?: string | null; cost?: number | null; warrantyUntil?: string | null; resultNotes?: string | null; fileIds?: string[] };
export type CompletionBody = { completedAt?: string | null; result: string; workshopId?: string | null; providerName?: string | null; repairType?: string | null; treadPattern?: string | null; newTreadDepthMm?: number | null; cost?: number | null; warrantyUntil?: string | null; resultNotes?: string | null; storageLocation?: string | null; fileIds?: string[] };
export type DisposalBody = { performedAt?: string | null; reason: string; destination?: string | null; notes?: string | null; fileIds?: string[] };
export type StockBody = { performedAt?: string | null; storageLocation?: string | null; notes?: string | null };
export type CostBody = { type: string; incurredOn: string; amount: number; description?: string | null };

export const tireModelsApi = {
  useList: (includeInactive = false, enabled = true) =>
    useQuery({ queryKey: ['tire-models', includeInactive], queryFn: () => get<TireModel[]>('/tire-models', { includeInactive: includeInactive ? 'true' : undefined }), enabled }),
  useBrands: () => useQuery({ queryKey: ['tire-models', 'brands'], queryFn: () => get<string[]>('/tire-models/brands') }),
  useSave: (id?: string) =>
    useOperation((body: Omit<TireModel, 'id' | 'tireCount'>) =>
      (id ? api.put<TireModel>(`/tire-models/${id}`, body) : api.post<TireModel>('/tire-models', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/tire-models/${id}`).then(() => undefined)),
};

export const tireLayoutsApi = {
  useList: (target?: string, includeInactive = false) =>
    useQuery({ queryKey: ['tire-layouts', target, includeInactive], queryFn: () => get<TireLayout[]>('/tire-layouts', { target, includeInactive: includeInactive ? 'true' : undefined }) }),
  useSave: (id?: string) =>
    useOperation((body: { name: string; target: string; description: string | null; spareCount: number; isActive: boolean; axles: Omit<TireLayout['axles'][number], 'number'>[] }) =>
      (id ? api.put<TireLayout>(`/tire-layouts/${id}`, body) : api.post<TireLayout>('/tire-layouts', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/tire-layouts/${id}`).then(() => undefined)),
};

export const tireSettingsApi = {
  useGet: (enabled = true) => useQuery({ queryKey: ['tire-settings'], queryFn: () => get<TireSettings>('/tire-settings'), enabled }),
  useSave: () => useOperation((body: TireSettings) => api.put<TireSettings>('/tire-settings', body).then((r) => r.data)),
};

export const tiresApi = {
  useList: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['tires', params], queryFn: () => get<PagedResult<TireListItem>>('/tires', params), placeholderData: keepPreviousData, enabled }),
  useDetail: (id: string | undefined) => useQuery({ queryKey: ['tires', 'detail', id], queryFn: () => get<Tire>(`/tires/${id}`), enabled: !!id }),
  useSave: (id?: string) =>
    useOperation((body: TireRequest) => (id ? api.put<Tire>(`/tires/${id}`, body) : api.post<Tire>('/tires', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/tires/${id}`).then(() => undefined)),
  useHistory: (id: string, params: ListParams) =>
    useQuery({ queryKey: ['tires', id, 'history', params], queryFn: () => get<PagedResult<HistoryEntry>>(`/tires/${id}/history`, params), placeholderData: keepPreviousData }),
  useInstallations: (id: string, params: ListParams) =>
    useQuery({ queryKey: ['tires', id, 'installations', params], queryFn: () => get<PagedResult<TireInstallation>>(`/tires/${id}/installations`, params), placeholderData: keepPreviousData }),
  useInspections: (id: string, params: ListParams) =>
    useQuery({ queryKey: ['tires', id, 'inspections', params], queryFn: () => get<PagedResult<TireInspection>>(`/tires/${id}/inspections`, params), placeholderData: keepPreviousData }),
  useServiceOrders: (id: string) => useQuery({ queryKey: ['tires', id, 'service-orders'], queryFn: () => get<TireServiceOrder[]>(`/tires/${id}/service-orders`) }),
  useCosts: (id: string, enabled: boolean) => useQuery({ queryKey: ['tires', id, 'costs'], queryFn: () => get<TireCost[]>(`/tires/${id}/costs`), enabled }),
  useCompatibility: (id: string | null, target: { vehicleId?: string | null; implementId?: string | null }, positionCode: string | null) =>
    useQuery({
      queryKey: ['tires', id, 'compatibility', target.vehicleId, target.implementId, positionCode],
      queryFn: () => get<TireCompatibility>(`/tires/${id}/compatibility`, { vehicleId: target.vehicleId, implementId: target.implementId, positionCode }),
      enabled: !!id && !!positionCode && !!(target.vehicleId || target.implementId),
    }),
  useInstall: (id: string) => useOperation((body: InstallBody) => api.post<Tire>(`/tires/${id}/install`, body).then((r) => r.data)),
  useRemoveFromAsset: (id: string) => useOperation((body: RemovalBody) => api.post<Tire>(`/tires/${id}/remove`, body).then((r) => r.data)),
  useReplace: (id: string) => useOperation((body: ReplaceBody) => api.post<Tire>(`/tires/${id}/replace`, body).then((r) => r.data)),
  useTransfer: (id: string) => useOperation((body: TransferBody) => api.post<Tire>(`/tires/${id}/transfer`, body).then((r) => r.data)),
  useEvaluation: (id: string) => useOperation((body: StockBody) => api.post<Tire>(`/tires/${id}/evaluation`, body).then((r) => r.data)),
  useReturnToStock: (id: string) => useOperation((body: StockBody) => api.post<Tire>(`/tires/${id}/return-to-stock`, body).then((r) => r.data)),
  useDispose: (id: string) => useOperation((body: DisposalBody) => api.post<Tire>(`/tires/${id}/dispose`, body).then((r) => r.data)),
  useInspect: (id: string) => useOperation((body: InspectionBody) => api.post<TireInspection>(`/tires/${id}/inspections`, body).then((r) => r.data)),
  useSendToService: (id: string) => useOperation((body: ServiceBody) => api.post<TireServiceOrder>(`/tires/${id}/service-orders`, body).then((r) => r.data)),
  useAddCost: (id: string) => useOperation((body: CostBody) => api.post<Tire>(`/tires/${id}/costs`, body).then((r) => r.data)),
  useDeleteCost: (id: string) => useOperation((costId: string) => api.delete<Tire>(`/tires/${id}/costs/${costId}`).then((r) => r.data)),
};

export const tireOperationsApi = {
  useRotate: () => useOperation((body: RotationBody) => api.post<AssetTires>('/tire-rotations', body).then((r) => r.data)),
  useCompleteService: (orderId: string) =>
    useOperation((body: CompletionBody) => api.post<TireServiceOrder>(`/tire-service-orders/${orderId}/complete`, body).then((r) => r.data)),
  useCancelService: (orderId: string) =>
    useOperation((reason: string) => api.post<TireServiceOrder>(`/tire-service-orders/${orderId}/cancel`, { reason }).then((r) => r.data)),
  useCorrectInstallation: (installationId: string) =>
    useOperation((body: { installedOdometerKm: number | null; removedOdometerKm: number | null; reason: string }) =>
      api.post<TireInstallation>(`/tire-installations/${installationId}/correct`, body).then((r) => r.data)),
  useReviewAnomaly: () =>
    useOperation(({ id, notes }: { id: string; notes: string }) => api.post<Tire>(`/tire-anomalies/${id}/review`, { notes }).then((r) => r.data)),
};

/** The tires of a vehicle or an implement (one concept for both — seção 10). */
export type AssetRef = { kind: 'vehicles' | 'implements'; id: string };

export const assetTiresApi = {
  useGet: (asset: AssetRef) => useQuery({ queryKey: ['asset-tires', asset.kind, asset.id], queryFn: () => get<AssetTires>(`/${asset.kind}/${asset.id}/tires`) }),
  useSetLayout: (asset: AssetRef) =>
    useOperation((layoutId: string | null) => api.put<AssetTires>(`/${asset.kind}/${asset.id}/tires/layout`, { layoutId }).then((r) => r.data)),
};

export const tireAnalyticsApi = {
  useDashboard: () => useQuery({ queryKey: ['tire-dashboard'], queryFn: () => get<TireDashboard>('/tires/dashboard') }),
  useInventory: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['tire-report', 'inventory', params], queryFn: () => get<TireReportPage<TireListItem>>('/tires/reports/inventory', params), placeholderData: keepPreviousData, enabled }),
  useLifecycle: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['tire-report', 'lifecycle', params], queryFn: () => get<TireReportPage<LifecycleReportRow>>('/tires/reports/lifecycle', params), placeholderData: keepPreviousData, enabled }),
  useInspections: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['tire-report', 'inspections', params], queryFn: () => get<TireReportPage<InspectionReportRow>>('/tires/reports/inspections', params), placeholderData: keepPreviousData, enabled }),
  useCosts: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['tire-report', 'costs', params], queryFn: () => get<TireReportPage<CostReportRow>>('/tires/reports/costs', params), placeholderData: keepPreviousData, enabled }),
};
