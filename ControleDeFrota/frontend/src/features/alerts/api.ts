import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { ListParams, PagedResult } from '../../api/crud';
import type {
  AlertStatus, AlertSummary, AttentionResponse, AutomationCatalog, AutomationExecution, AutomationRule, AutomationRuleRequest,
  AutomationRunResult, FleetAlert, NotificationPage,
} from './alerts';

const clean = (params: ListParams) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));

const get = <T>(url: string, params?: ListParams) => api.get<T>(url, { params: params && clean(params) }).then((r) => r.data);

/** Alerts feed the dashboard, the bell and the attention list: refresh everything they touch. */
function useInvalidatingMutation<TBody, TResult>(fn: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['alerts'] });
      void queryClient.invalidateQueries({ queryKey: ['notifications'] });
      void queryClient.invalidateQueries({ queryKey: ['attention'] });
      void queryClient.invalidateQueries({ queryKey: ['automation'] });
    },
  });
}

export const alertsApi = {
  useList: (params: ListParams) =>
    useQuery({ queryKey: ['alerts', 'list', params], queryFn: () => get<PagedResult<FleetAlert>>('/alerts', params), placeholderData: keepPreviousData }),
  useTop: (enabled: boolean) =>
    useQuery({ queryKey: ['alerts', 'top'], queryFn: () => get<PagedResult<FleetAlert>>('/alerts', { pageSize: 5 }), enabled }),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['alerts', 'detail', id], queryFn: () => get<FleetAlert>(`/alerts/${id}`), enabled: !!id }),
  useSummary: (enabled = true) => useQuery({ queryKey: ['alerts', 'summary'], queryFn: () => get<AlertSummary>('/alerts/summary'), enabled }),
  useMarkRead: () => useInvalidatingMutation((id: string) => api.post(`/alerts/${id}/read`).then(() => undefined)),
  useChangeStatus: (id: string) =>
    useInvalidatingMutation((body: { status: AlertStatus; notes?: string | null }) =>
      api.post<FleetAlert>(`/alerts/${id}/status`, body).then((r) => r.data)),
};

export const attentionApi = {
  useAttention: () => useQuery({ queryKey: ['attention'], queryFn: () => get<AttentionResponse>('/dashboard/attention') }),
};

export const notificationsApi = {
  /** Polls once a minute while the app is open; in-app only (no e-mail/push in this version). */
  useUnreadCount: (enabled: boolean) =>
    useQuery({ queryKey: ['notifications', 'unread'], queryFn: () => get<number>('/notifications/unread-count'), enabled, refetchInterval: 60_000 }),
  useList: (enabled: boolean) =>
    useQuery({ queryKey: ['notifications', 'list'], queryFn: () => get<NotificationPage>('/notifications', { page: 1 }), enabled }),
  useMarkRead: () => useInvalidatingMutation((id: string) => api.post(`/notifications/${id}/read`).then(() => undefined)),
  useMarkAllRead: () => useInvalidatingMutation(() => api.post('/notifications/read-all').then(() => undefined)),
};

export const automationApi = {
  useCatalog: () => useQuery({ queryKey: ['automation', 'catalog'], queryFn: () => get<AutomationCatalog>('/automation/catalog'), staleTime: Infinity }),
  useRules: () => useQuery({ queryKey: ['automation', 'rules'], queryFn: () => get<AutomationRule[]>('/automation/rules') }),
  useExecutions: (ruleId: string | null) =>
    useQuery({ queryKey: ['automation', 'executions', ruleId], queryFn: () => get<AutomationExecution[]>(`/automation/rules/${ruleId}/executions`), enabled: !!ruleId }),
  useSave: (id?: string) =>
    useInvalidatingMutation((body: AutomationRuleRequest) =>
      (id ? api.put<AutomationRule>(`/automation/rules/${id}`, body) : api.post<AutomationRule>('/automation/rules', body)).then((r) => r.data)),
  useRemove: () => useInvalidatingMutation((id: string) => api.delete(`/automation/rules/${id}`).then(() => undefined)),
  useRun: () => useInvalidatingMutation(() => api.post<AutomationRunResult>('/automation/run').then((r) => r.data)),
};
