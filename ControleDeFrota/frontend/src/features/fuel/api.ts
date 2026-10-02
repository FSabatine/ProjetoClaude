import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { ListParams, PagedResult } from '../../api/crud';
import type {
  ConsumptionReportRow,
  CostReportRow,
  FuelDashboard,
  FuelPrice,
  FuelPriceHint,
  FuelSettings,
  FuelStation,
  FuelType,
  Fueling,
  FuelingCorrectionRequest,
  FuelingFormDefaults,
  FuelingListItem,
  FuelingRequest,
  PriceReportRow,
  ReportPage,
  StationReportRow,
  VehicleFuel,
} from './fuel';

const clean = (params: ListParams) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));

const get = <T>(url: string, params?: ListParams) => api.get<T>(url, { params: params && clean(params) }).then((r) => r.data);

/** A fueling changes mileage, consumption, dashboards and the vehicle hub at once: refresh everything cached (same as Phases 2/3). */
function useOperation<TBody, TResult>(fn: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries() });
}

export const fuelTypesApi = {
  useList: (includeInactive = false) =>
    useQuery({ queryKey: ['fuel-types', includeInactive], queryFn: () => get<FuelType[]>('/fuel-types', { includeInactive: includeInactive ? 'true' : undefined }) }),
  useSave: (id?: string) =>
    useOperation((body: Omit<FuelType, 'id' | 'fuelingCount'>) =>
      (id ? api.put<FuelType>(`/fuel-types/${id}`, body) : api.post<FuelType>('/fuel-types', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/fuel-types/${id}`).then(() => undefined)),
};

export const fuelStationsApi = {
  useList: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['fuel-stations', params], queryFn: () => get<PagedResult<FuelStation>>('/fuel-stations', params), placeholderData: keepPreviousData, enabled }),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['fuel-stations', 'detail', id], queryFn: () => get<FuelStation>(`/fuel-stations/${id}`), enabled: !!id }),
  useSave: (id?: string) =>
    useOperation((body: Omit<FuelStation, 'id' | 'fuelingCount' | 'lastFueledOn' | 'createdAt' | 'updatedAt'>) =>
      (id ? api.put<FuelStation>(`/fuel-stations/${id}`, body) : api.post<FuelStation>('/fuel-stations', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/fuel-stations/${id}`).then(() => undefined)),
  usePrices: (id: string, enabled = true) =>
    useQuery({ queryKey: ['fuel-stations', id, 'prices'], queryFn: () => get<FuelPrice[]>(`/fuel-stations/${id}/prices`), enabled }),
  useAddPrice: (id: string) =>
    useOperation((body: { fuelTypeId: string; price: number; effectiveFrom: string; notes: string | null }) =>
      api.post<FuelPrice>(`/fuel-stations/${id}/prices`, body).then((r) => r.data)),
  useRemovePrice: (id: string) => useOperation((priceId: string) => api.delete(`/fuel-stations/${id}/prices/${priceId}`).then(() => undefined)),
};

export const fuelSettingsApi = {
  useGet: () => useQuery({ queryKey: ['fuel-settings'], queryFn: () => get<FuelSettings>('/fuel-settings') }),
  useSave: () => useOperation((body: FuelSettings) => api.put<FuelSettings>('/fuel-settings', body).then((r) => r.data)),
};

export const fuelingsApi = {
  useList: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['fuelings', params], queryFn: () => get<PagedResult<FuelingListItem>>('/fuelings', params), placeholderData: keepPreviousData, enabled }),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['fuelings', 'detail', id], queryFn: () => get<Fueling>(`/fuelings/${id}`), enabled: !!id }),
  useFormDefaults: (vehicleId: string | null) =>
    useQuery({ queryKey: ['fuelings', 'defaults', vehicleId], queryFn: () => get<FuelingFormDefaults>('/fuelings/form-defaults', { vehicleId }), enabled: !!vehicleId }),
  usePriceHint: (fuelStationId: string | null, fuelTypeId: string | null) =>
    useQuery({
      queryKey: ['fuelings', 'price-hint', fuelStationId, fuelTypeId],
      // 204 (no known price) arrives as an empty body.
      queryFn: () => get<FuelPriceHint | ''>('/fuelings/price-hint', { fuelStationId, fuelTypeId }).then((d) => (d ? d : null)),
      enabled: !!fuelStationId && !!fuelTypeId,
    }),
  useCreate: () => useOperation((body: FuelingRequest) => api.post<Fueling>('/fuelings', body).then((r) => r.data)),
  useCorrect: (id: string) => useOperation((body: FuelingCorrectionRequest) => api.post<Fueling>(`/fuelings/${id}/correct`, body).then((r) => r.data)),
  useCancel: (id: string) => useOperation((reason: string) => api.post<Fueling>(`/fuelings/${id}/cancel`, { reason }).then((r) => r.data)),
  useReview: (id: string) => useOperation((reason: string) => api.post<Fueling>(`/fuelings/${id}/review`, { reason }).then((r) => r.data)),
  useAttach: (id: string) => useOperation((fileIds: string[]) => api.post<Fueling>(`/fuelings/${id}/files`, { fileIds }).then((r) => r.data)),
};

export const fuelAnalyticsApi = {
  useDashboard: (params: ListParams) =>
    useQuery({ queryKey: ['fuel-dashboard', params], queryFn: () => get<FuelDashboard>('/fuel/dashboard', params), placeholderData: keepPreviousData }),
  useVehicle: (vehicleId: string, params: ListParams) =>
    useQuery({ queryKey: ['vehicle-fuel', vehicleId, params], queryFn: () => get<VehicleFuel>(`/vehicles/${vehicleId}/fuel`, params), placeholderData: keepPreviousData }),
  useConsumption: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['fuel-report', 'consumption', params], queryFn: () => get<ReportPage<ConsumptionReportRow>>('/fuel/reports/consumption', params), placeholderData: keepPreviousData, enabled }),
  useCosts: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['fuel-report', 'costs', params], queryFn: () => get<ReportPage<CostReportRow>>('/fuel/reports/costs', params), placeholderData: keepPreviousData, enabled }),
  useStations: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['fuel-report', 'stations', params], queryFn: () => get<ReportPage<StationReportRow>>('/fuel/reports/stations', params), placeholderData: keepPreviousData, enabled }),
  usePrices: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['fuel-report', 'prices', params], queryFn: () => get<ReportPage<PriceReportRow>>('/fuel/reports/prices', params), placeholderData: keepPreviousData, enabled }),
};
