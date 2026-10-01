import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { ListParams, PagedResult } from '../../api/crud';
import type {
  HourMeterReading,
  MaintenancePlan,
  MaintenancePlanListItem,
  MaintenancePlanRequest,
  MaintenanceRequest,
  MaintenanceRequestRequest,
  MaintenanceScheduleItem,
  RepeatedProblem,
  WorkOrder,
  WorkOrderListItem,
  WorkOrderRequest,
  WorkOrderStatus,
} from './maintenance';

const clean = (params: ListParams) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));

const get = <T>(url: string, params?: ListParams) => api.get<T>(url, { params: params && clean(params) }).then((r) => r.data);

function usePaged<T>(key: unknown[], url: string, params: ListParams, enabled = true) {
  return useQuery({ queryKey: [...key, params], queryFn: () => get<PagedResult<T>>(url, params), placeholderData: keepPreviousData, enabled });
}

/** Maintenance changes ripple across screens (vehicle hub, dashboard, lists), so a successful mutation refreshes everything cached. */
function useOperation<TBody, TResult>(fn: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries() });
}

export const hourMeterApi = {
  useReadings: (vehicleId: string, params: ListParams = {}) =>
    usePaged<HourMeterReading>(['hour-meter', vehicleId], `/vehicles/${vehicleId}/hour-meter-readings`, params),
  useRecord: (vehicleId: string) =>
    useOperation((body: { hours: number; readAt: string | null; notes: string | null; isCorrection: boolean }) =>
      api.post<HourMeterReading>(`/vehicles/${vehicleId}/hour-meter-readings`, body).then((r) => r.data)),
  useReview: () =>
    useOperation(({ id, approve, notes }: { id: string; approve: boolean; notes: string | null }) =>
      api.post<HourMeterReading>(`/hour-meter-readings/${id}/${approve ? 'approve' : 'reject'}`, { notes }).then((r) => r.data)),
};

/** Not a PagedResult (the backend returns a plain array) — same shape as checklistTemplatesApi. */
export const maintenancePlansApi = {
  useList: (includeInactive = false) =>
    useQuery({
      queryKey: ['maintenance-plans', includeInactive],
      queryFn: () => get<MaintenancePlanListItem[]>('/maintenance-plans', { includeInactive: includeInactive ? 'true' : undefined }),
    }),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['maintenance-plans', 'detail', id], queryFn: () => get<MaintenancePlan>(`/maintenance-plans/${id}`), enabled: !!id }),
  useSave: (id?: string) =>
    useOperation((body: MaintenancePlanRequest) =>
      (id ? api.put<MaintenancePlan>(`/maintenance-plans/${id}`, body) : api.post<MaintenancePlan>('/maintenance-plans', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/maintenance-plans/${id}`).then(() => undefined)),
};

export const vehicleMaintenanceApi = {
  useSchedule: (vehicleId: string, enabled = true) =>
    useQuery({ queryKey: ['maintenance-schedule', vehicleId], queryFn: () => get<MaintenanceScheduleItem[]>(`/vehicles/${vehicleId}/maintenance/schedule`), enabled }),
  useHistory: (vehicleId: string, params: ListParams, enabled = true) =>
    usePaged<WorkOrderListItem>(['work-orders', 'vehicle', vehicleId], `/vehicles/${vehicleId}/maintenance/history`, params, enabled),
  useRepeatedProblems: (vehicleId: string, enabled = true) =>
    useQuery({ queryKey: ['repeated-problems', vehicleId], queryFn: () => get<RepeatedProblem[]>(`/vehicles/${vehicleId}/maintenance/repeated-problems`), enabled }),
};

export const maintenanceRequestsApi = {
  useList: (params: ListParams, enabled = true) => usePaged<MaintenanceRequest>(['maintenance-requests'], '/maintenance-requests', params, enabled),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['maintenance-requests', 'detail', id], queryFn: () => get<MaintenanceRequest>(`/maintenance-requests/${id}`), enabled: !!id }),
  useCreate: () => useOperation((body: MaintenanceRequestRequest) => api.post<MaintenanceRequest>('/maintenance-requests', body).then((r) => r.data)),
  useApprove: () => useOperation((id: string) => api.post<MaintenanceRequest>(`/maintenance-requests/${id}/approve`).then((r) => r.data)),
  useReject: () =>
    useOperation(({ id, reason }: { id: string; reason: string }) =>
      api.post<MaintenanceRequest>(`/maintenance-requests/${id}/reject`, { reason }).then((r) => r.data)),
};

export const workOrdersApi = {
  useList: (params: ListParams, enabled = true) => usePaged<WorkOrderListItem>(['work-orders'], '/work-orders', params, enabled),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['work-orders', 'detail', id], queryFn: () => get<WorkOrder>(`/work-orders/${id}`), enabled: !!id }),
  useSave: (id?: string) =>
    useOperation((body: WorkOrderRequest) => (id ? api.put<WorkOrder>(`/work-orders/${id}`, body) : api.post<WorkOrder>('/work-orders', body)).then((r) => r.data)),
  useChangeStatus: (id: string) =>
    useOperation((body: { status: WorkOrderStatus; resolution: string | null; reason: string | null; odometerKm: number | null; hourMeter: number | null }) =>
      api.post<WorkOrder>(`/work-orders/${id}/status`, body).then((r) => r.data)),
  useCompleteItem: (id: string) =>
    useOperation(({ itemId, ...body }: { itemId: string; skip: boolean; notes: string | null }) =>
      api.post<WorkOrder>(`/work-orders/${id}/items/${itemId}/complete`, body).then((r) => r.data)),
  useAddPart: (id: string) =>
    useOperation((body: { partName: string; partNumber: string | null; quantity: number; unitCost: number; supplier: string | null; notes: string | null }) =>
      api.post<WorkOrder>(`/work-orders/${id}/parts`, body).then((r) => r.data)),
  useRemovePart: (id: string) => useOperation((partId: string) => api.delete<WorkOrder>(`/work-orders/${id}/parts/${partId}`).then((r) => r.data)),
  useAddLabor: (id: string) =>
    useOperation((body: { technicianName: string; hours: number; hourlyRate: number; description: string | null }) =>
      api.post<WorkOrder>(`/work-orders/${id}/labor`, body).then((r) => r.data)),
  useRemoveLabor: (id: string) => useOperation((laborId: string) => api.delete<WorkOrder>(`/work-orders/${id}/labor/${laborId}`).then((r) => r.data)),
};
