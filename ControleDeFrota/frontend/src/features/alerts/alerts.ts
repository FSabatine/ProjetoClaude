import type { StatusOption } from '../../components/common';

export type AlertSeverity = 'Info' | 'Warning' | 'Critical';
export type AlertStatus = 'New' | 'Read' | 'InProgress' | 'Resolved' | 'Dismissed';
export type AlertCategory = 'Maintenance' | 'Fuel' | 'Tires' | 'Finance' | 'Documents' | 'Operations';
export type AutomationTrigger =
  | 'MaintenanceOverdue' | 'MaintenanceDueSoon' | 'FuelConsumptionAbnormal' | 'VehicleCostAboveAverage' | 'BudgetThreshold'
  | 'TireTreadLow' | 'ExpenseOverdue' | 'DocumentExpiring' | 'OperationalEvent';
export type NotifyMode = 'None' | 'EveryoneWhoCanSee' | 'SpecificUser';
export type TriggerKind = 'Scheduled' | 'Event';

export interface FleetAlert {
  id: string;
  trigger: AutomationTrigger;
  category: AlertCategory;
  severity: AlertSeverity;
  status: AlertStatus;
  priority: number;
  title: string;
  explanation: string;
  evidence: string;
  recommendedAction: string;
  entityType: string;
  entityId: string;
  vehicleId: string | null;
  licensePlate: string | null;
  tab: string | null;
  ruleId: string | null;
  ruleName: string | null;
  detectedAt: string;
  lastDetectedAt: string;
  detectionCount: number;
  recurrenceCount: number;
  readAt: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  closedAt: string | null;
  closedByName: string | null;
  closingNotes: string | null;
  autoResolved: boolean;
  nextStatuses: AlertStatus[];
}

export interface AlertSummary {
  open: number;
  new: number;
  critical: number;
  warning: number;
  info: number;
  assignedToMe: number;
  byCategory: { key: AlertCategory; count: number }[];
  byTrigger: { key: AutomationTrigger; count: number }[];
  lastCheckedAt: string | null;
}

export interface AttentionItem {
  key: string;
  label: string;
  count: number;
  severity: AlertSeverity;
  category: AlertCategory;
  link: string;
  source: 'alert' | 'queue';
}

export interface AttentionResponse {
  items: AttentionItem[];
  totalCount: number;
  alertsCheckedAt: string | null;
}

export interface UserNotification {
  id: string;
  severity: AlertSeverity;
  title: string;
  message: string;
  link: string | null;
  createdAt: string;
  readAt: string | null;
}

export interface NotificationPage {
  items: UserNotification[];
  unreadCount: number;
  hasMore: boolean;
}

export interface AutomationExecution {
  startedAt: string;
  finishedAt: string;
  source: 'Scheduled' | 'Manual';
  evaluated: number;
  alertsCreated: number;
  alertsUpdated: number;
  alertsResolved: number;
  notificationsCreated: number;
  error: string | null;
}

export interface AutomationRule {
  id: string;
  name: string;
  description: string | null;
  trigger: AutomationTrigger;
  kind: TriggerKind;
  eventType: string | null;
  threshold: number | null;
  lookbackDays: number | null;
  severity: AlertSeverity;
  createAlert: boolean;
  notifyMode: NotifyMode;
  notifyUserId: string | null;
  notifyUserName: string | null;
  isActive: boolean;
  isSystemDefault: boolean;
  openAlerts: number;
  lastExecution: AutomationExecution | null;
}

export interface AutomationRuleRequest {
  name: string;
  description: string | null;
  trigger: AutomationTrigger;
  eventType: string | null;
  threshold: number | null;
  lookbackDays: number | null;
  severity: AlertSeverity;
  createAlert: boolean;
  notifyMode: NotifyMode;
  notifyUserId: string | null;
  isActive: boolean;
}

export interface TriggerDefinition {
  trigger: AutomationTrigger;
  kind: TriggerKind;
  category: AlertCategory;
  name: string;
  description: string;
  defaultSeverity: AlertSeverity;
  thresholdLabel: string | null;
  thresholdUnit: string | null;
  defaultThreshold: number | null;
  minThreshold: number | null;
  maxThreshold: number | null;
  lookbackLabel: string | null;
  defaultLookbackDays: number | null;
  defaultNotifyMode: NotifyMode;
}

export interface AutomationCatalog {
  triggers: TriggerDefinition[];
  events: string[];
}

export interface AutomationRunResult {
  rulesEvaluated: number;
  alertsCreated: number;
  alertsUpdated: number;
  alertsResolved: number;
  notificationsCreated: number;
  eventsProcessed: number;
  rulesFailed: number;
}

export const ALERT_SEVERITY: Record<AlertSeverity, StatusOption> = {
  Critical: { label: 'Crítico', color: 'red' },
  Warning: { label: 'Atenção', color: 'orange' },
  Info: { label: 'Informativo', color: 'blue' },
};

export const ALERT_STATUS: Record<AlertStatus, StatusOption> = {
  New: { label: 'Novo', color: 'blue' },
  Read: { label: 'Lido', color: 'gray' },
  InProgress: { label: 'Em andamento', color: 'yellow' },
  Resolved: { label: 'Resolvido', color: 'teal' },
  Dismissed: { label: 'Descartado', color: 'gray' },
};

export const ALERT_CATEGORY: Record<AlertCategory, { label: string }> = {
  Maintenance: { label: 'Manutenção' },
  Fuel: { label: 'Combustível' },
  Tires: { label: 'Pneus' },
  Finance: { label: 'Financeiro' },
  Documents: { label: 'Documentos' },
  Operations: { label: 'Operação' },
};

export const TRIGGER_LABEL: Record<AutomationTrigger, { label: string }> = {
  MaintenanceOverdue: { label: 'Manutenção preventiva atrasada' },
  MaintenanceDueSoon: { label: 'Manutenção preventiva próxima' },
  FuelConsumptionAbnormal: { label: 'Consumo fora do padrão' },
  VehicleCostAboveAverage: { label: 'Custo acima da média do tipo' },
  BudgetThreshold: { label: 'Orçamento perto do limite' },
  TireTreadLow: { label: 'Pneu com sulco baixo' },
  ExpenseOverdue: { label: 'Despesa em atraso' },
  DocumentExpiring: { label: 'Documento vencido ou vencendo' },
  OperationalEvent: { label: 'Quando um fato acontecer' },
};

export const NOTIFY_MODE: Record<NotifyMode, { label: string }> = {
  None: { label: 'Ninguém (só o alerta)' },
  EveryoneWhoCanSee: { label: 'Todos que podem ver o alerta' },
  SpecificUser: { label: 'Um usuário específico' },
};

/** Facts that may trigger a rule (mirror of AutomationTriggerCatalog.NotifiableEvents). */
export const EVENT_LABEL: Record<string, string> = {
  ChecklistFailed: 'Checklist reprovado',
  OccurrenceCreated: 'Ocorrência registrada',
  MileageAnomalyDetected: 'Leitura de hodômetro suspeita',
  HourMeterAnomalyDetected: 'Leitura de horímetro suspeita',
  MaintenanceRequestCreated: 'Solicitação de manutenção aberta',
  WorkOrderCompleted: 'Ordem de serviço concluída',
  FuelingMarkedForReview: 'Abastecimento marcado para revisão',
  FuelConsumptionAnomalyDetected: 'Consumo fora do esperado em um abastecimento',
  TireInspectionFailed: 'Inspeção de pneu reprovada',
  TireAnomalyDetected: 'Sinal de "requer revisão" em pneu',
  DocumentExpired: 'Documento venceu',
  BudgetExceeded: 'Orçamento ultrapassado',
};

/** Route of the record an alert is about (hub tabs use pt-BR names). */
export function alertEntityLink(a: Pick<FleetAlert, 'entityType' | 'entityId' | 'vehicleId' | 'tab'>): string | null {
  const tab = a.tab ? `?aba=${a.tab}` : '';
  switch (a.entityType) {
    case 'Vehicle': return `/veiculos/${a.entityId}${tab}`;
    case 'Driver': return `/motoristas/${a.entityId}${tab}`;
    case 'Implement': return `/implementos/${a.entityId}`;
    case 'Tire': return `/pneus/${a.entityId}`;
    case 'Expense': return `/financeiro/despesas/${a.entityId}/editar`;
    case 'Budget': return '/financeiro/orcamentos';
    case 'Occurrence': return `/ocorrencias/${a.entityId}`;
    case 'ChecklistExecution': return `/checklists/${a.entityId}`;
    case 'Fueling': return `/abastecimentos/${a.entityId}`;
    case 'WorkOrder': return `/ordens-servico/${a.entityId}`;
    case 'MaintenanceRequest': return '/solicitacoes-manutencao';
    case 'Company': return '/minha-empresa';
    default: return a.vehicleId ? `/veiculos/${a.vehicleId}` : null;
  }
}
