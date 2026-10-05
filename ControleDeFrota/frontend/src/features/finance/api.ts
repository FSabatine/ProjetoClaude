import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { ListParams, PagedResult } from '../../api/crud';
import type {
  Budget,
  BudgetVsActual,
  CategoryCost,
  CostCenter,
  CostCenterCost,
  Expense,
  ExpenseCategory,
  ExpenseRequest,
  FinanceDashboard,
  MonthlyCost,
  RecurringExpense,
  VehicleCostBreakdown,
  VehicleCostPerKm,
  VehicleRankingRow,
  VehicleTco,
} from './finance';

const clean = (params: ListParams) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));

const get = <T>(url: string, params?: ListParams) => api.get<T>(url, { params: params && clean(params) }).then((r) => r.data);

/** A financial mutation touches reports/dashboard/vehicle panels across the app: refresh everything cached. */
function useOperation<TBody, TResult>(fn: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries() });
}

export const costCentersApi = {
  useList: (includeInactive = false) =>
    useQuery({ queryKey: ['cost-centers', includeInactive], queryFn: () => get<CostCenter[]>('/cost-centers', { includeInactive: includeInactive ? 'true' : undefined }) }),
  useSave: (id?: string) =>
    useOperation((body: Omit<CostCenter, 'id' | 'parentCostCenterName'>) =>
      (id ? api.put<CostCenter>(`/cost-centers/${id}`, body) : api.post<CostCenter>('/cost-centers', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/cost-centers/${id}`).then(() => undefined)),
};

export const expenseCategoriesApi = {
  useList: (includeInactive = false) =>
    useQuery({ queryKey: ['expense-categories', includeInactive], queryFn: () => get<ExpenseCategory[]>('/expense-categories', { includeInactive: includeInactive ? 'true' : undefined }) }),
  useSave: (id?: string) =>
    useOperation((body: Omit<ExpenseCategory, 'id' | 'parentCategoryName' | 'isSystemCategory'>) =>
      (id ? api.put<ExpenseCategory>(`/expense-categories/${id}`, body) : api.post<ExpenseCategory>('/expense-categories', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/expense-categories/${id}`).then(() => undefined)),
};

export const expensesApi = {
  useList: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['expenses', params], queryFn: () => get<PagedResult<Expense>>('/expenses', params), placeholderData: keepPreviousData, enabled }),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['expenses', 'detail', id], queryFn: () => get<Expense>(`/expenses/${id}`), enabled: !!id }),
  useCreate: () => useOperation((body: ExpenseRequest) => api.post<Expense>('/expenses', body).then((r) => r.data)),
  useUpdate: (id: string) => useOperation((body: ExpenseRequest) => api.put<Expense>(`/expenses/${id}`, body).then((r) => r.data)),
  useRegisterPayment: (id: string) =>
    useOperation((body: { paidAmount: number; paymentDate: string }) => api.post<Expense>(`/expenses/${id}/payment`, body).then((r) => r.data)),
  useCancel: (id: string) => useOperation((reason: string) => api.post<Expense>(`/expenses/${id}/cancel`, { reason }).then((r) => r.data)),
};

export const recurringExpensesApi = {
  useList: (includeInactive = false) =>
    useQuery({ queryKey: ['recurring-expenses', includeInactive], queryFn: () => get<RecurringExpense[]>('/recurring-expenses', { includeInactive: includeInactive ? 'true' : undefined }) }),
  useSave: (id?: string) =>
    useOperation((body: Record<string, unknown>) =>
      (id ? api.put<RecurringExpense>(`/recurring-expenses/${id}`, body) : api.post<RecurringExpense>('/recurring-expenses', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/recurring-expenses/${id}`).then(() => undefined)),
};

export const budgetsApi = {
  useList: (year: number) => useQuery({ queryKey: ['budgets', year], queryFn: () => get<Budget[]>('/budgets', { year }) }),
  useVsActual: (year: number, month?: number | null) =>
    useQuery({ queryKey: ['budgets', 'vs-actual', year, month], queryFn: () => get<BudgetVsActual[]>('/budgets/vs-actual', { year, month }) }),
  useSave: (id?: string) =>
    useOperation((body: Record<string, unknown>) =>
      (id ? api.put<Budget>(`/budgets/${id}`, body) : api.post<Budget>('/budgets', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/budgets/${id}`).then(() => undefined)),
};

export const financeAnalyticsApi = {
  useDashboard: () => useQuery({ queryKey: ['finance-dashboard'], queryFn: () => get<FinanceDashboard>('/finance/dashboard') }),
  useRanking: (params: ListParams, enabled = true) =>
    useQuery({ queryKey: ['finance-ranking', params], queryFn: () => get<PagedResult<VehicleRankingRow>>('/finance/ranking', params), placeholderData: keepPreviousData, enabled }),
  useCostByCategory: (from: string, to: string) =>
    useQuery({ queryKey: ['finance-cost-by-category', from, to], queryFn: () => get<{ items: CategoryCost[]; isPartial: boolean }>('/finance/cost-by-category', { from, to }) }),
  useCostByCostCenter: (from: string, to: string) =>
    useQuery({ queryKey: ['finance-cost-by-cost-center', from, to], queryFn: () => get<{ items: CostCenterCost[]; isPartial: boolean }>('/finance/cost-by-cost-center', { from, to }) }),
  useMonthlyEvolution: (from: string, to: string, vehicleId?: string | null) =>
    useQuery({
      queryKey: ['finance-monthly-evolution', from, to, vehicleId],
      queryFn: () => get<{ items: MonthlyCost[]; isPartial: boolean }>('/finance/monthly-evolution', { from, to, vehicleId }),
    }),
  useVehicleBreakdown: (vehicleId: string, from: string, to: string) =>
    useQuery({ queryKey: ['finance-vehicle-breakdown', vehicleId, from, to], queryFn: () => get<VehicleCostBreakdown>(`/finance/vehicles/${vehicleId}/breakdown`, { from, to }) }),
  useVehicleCostPerKm: (vehicleId: string, from: string, to: string) =>
    useQuery({ queryKey: ['finance-vehicle-cost-per-km', vehicleId, from, to], queryFn: () => get<VehicleCostPerKm>(`/finance/vehicles/${vehicleId}/cost-per-km`, { from, to }) }),
  useVehicleTco: (vehicleId: string | null) =>
    useQuery({ queryKey: ['finance-vehicle-tco', vehicleId], queryFn: () => get<VehicleTco>(`/finance/vehicles/${vehicleId}/tco`), enabled: !!vehicleId }),
};
