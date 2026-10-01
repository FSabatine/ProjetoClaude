import { createResource } from '../../api/crud';
import type { StatusOption } from '../../components/common';
import { STATUS_COLOR } from '../../theme';
import type { VehicleType } from '../vehicles/vehicles';

export interface Address {
  street: string | null;
  number: string | null;
  complement: string | null;
  neighborhood: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
}

// Phase 3 enums → pt-BR labels and badge colors — the ONE place for these texts (fleet-development skill).

export const MAINTENANCE_TYPE = {
  Preventive: { label: 'Preventiva' },
  Corrective: { label: 'Corretiva' },
  Inspection: { label: 'Inspeção' },
};
export type MaintenanceType = keyof typeof MAINTENANCE_TYPE;

export const MAINTENANCE_PRIORITY = {
  Low: { label: 'Baixa', color: STATUS_COLOR.neutral },
  Medium: { label: 'Média', color: 'yellow' },
  High: { label: 'Alta', color: STATUS_COLOR.warning },
  Critical: { label: 'Crítica', color: STATUS_COLOR.danger },
} satisfies Record<string, StatusOption>;
export type MaintenancePriority = keyof typeof MAINTENANCE_PRIORITY;

export const WORKSHOP_STATUS = {
  Active: { label: 'Ativa', color: STATUS_COLOR.positive },
  Inactive: { label: 'Inativa', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type WorkshopStatus = keyof typeof WORKSHOP_STATUS;

export const MAINTENANCE_SCHEDULE_STATUS = {
  Scheduled: { label: 'Programada', color: STATUS_COLOR.neutral },
  DueSoon: { label: 'Vencendo', color: STATUS_COLOR.warning },
  Due: { label: 'Vence agora', color: 'yellow' },
  Overdue: { label: 'Atrasada', color: STATUS_COLOR.danger },
} satisfies Record<string, StatusOption>;
export type MaintenanceScheduleStatus = keyof typeof MAINTENANCE_SCHEDULE_STATUS;

export const MAINTENANCE_REQUEST_SOURCE = {
  Driver: { label: 'Motorista' },
  Checklist: { label: 'Checklist' },
  FleetManager: { label: 'Gestor de frota' },
  Occurrence: { label: 'Ocorrência' },
  AutomaticAlert: { label: 'Alerta automático' },
};
export type MaintenanceRequestSource = keyof typeof MAINTENANCE_REQUEST_SOURCE;

export const MAINTENANCE_REQUEST_STATUS = {
  Open: { label: 'Aberta', color: STATUS_COLOR.active },
  Converted: { label: 'Convertida em OS', color: STATUS_COLOR.positive },
  Rejected: { label: 'Rejeitada', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type MaintenanceRequestStatus = keyof typeof MAINTENANCE_REQUEST_STATUS;

export const WORK_ORDER_STATUS = {
  Draft: { label: 'Rascunho', color: STATUS_COLOR.neutral },
  Approved: { label: 'Aprovada', color: STATUS_COLOR.active },
  Scheduled: { label: 'Agendada', color: 'cyan' },
  InProgress: { label: 'Em andamento', color: STATUS_COLOR.warning },
  WaitingParts: { label: 'Aguardando peças', color: 'yellow' },
  Completed: { label: 'Concluída', color: STATUS_COLOR.positive },
  Cancelled: { label: 'Cancelada', color: STATUS_COLOR.neutral },
  Rejected: { label: 'Rejeitada', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type WorkOrderStatus = keyof typeof WORK_ORDER_STATUS;

/** Verb shown on the button that moves a work order to each status. */
export const WORK_ORDER_ACTION: Record<WorkOrderStatus, string> = {
  Draft: 'Voltar para rascunho',
  Approved: 'Aprovar',
  Scheduled: 'Agendar',
  InProgress: 'Iniciar execução',
  WaitingParts: 'Aguardar peças',
  Completed: 'Concluir ordem',
  Cancelled: 'Cancelar ordem',
  Rejected: 'Rejeitar ordem',
};

export const WORK_ORDER_ITEM_STATUS = {
  Pending: { label: 'Pendente', color: STATUS_COLOR.neutral },
  Done: { label: 'Concluído', color: STATUS_COLOR.positive },
  Skipped: { label: 'Dispensado', color: 'yellow' },
} satisfies Record<string, StatusOption>;
export type WorkOrderItemStatus = keyof typeof WORK_ORDER_ITEM_STATUS;

export const HOUR_METER_STATUS = {
  Valid: { label: 'Válida', color: STATUS_COLOR.positive },
  PendingReview: { label: 'Suspeita — em revisão', color: STATUS_COLOR.warning },
  Rejected: { label: 'Rejeitada', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type HourMeterStatus = keyof typeof HOUR_METER_STATUS;

// ---------------------------------------------------------------- types (mirror of the API DTOs)

export interface Workshop {
  id: string;
  name: string;
  document: string | null;
  phone: string | null;
  email: string | null;
  address: Address;
  specialties: string | null;
  status: WorkshopStatus;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface WorkshopRequest {
  name: string;
  document: string | null;
  phone: string | null;
  email: string | null;
  address: Address;
  specialties: string | null;
  status: WorkshopStatus;
  notes: string | null;
}

export const workshopsApi = createResource<Workshop, Workshop, WorkshopRequest>('/workshops');

export interface MaintenancePlanItem {
  id: string;
  serviceName: string;
  intervalKm: number | null;
  intervalMonths: number | null;
  intervalHours: number | null;
  graceKm: number | null;
  graceDays: number | null;
  graceHours: number | null;
  priority: MaintenancePriority;
  estimatedDurationMinutes: number | null;
  estimatedCost: number | null;
  isRequired: boolean;
  notes: string | null;
}

export type MaintenancePlanItemRequest = Omit<MaintenancePlanItem, 'id'> & { id: string | null };

export interface MaintenancePlanListItem {
  id: string;
  name: string;
  vehicleId: string | null;
  vehicleLicensePlate: string | null;
  vehicleType: VehicleType | null;
  isActive: boolean;
  itemCount: number;
}

export interface MaintenancePlan extends Omit<MaintenancePlanListItem, 'itemCount'> {
  items: MaintenancePlanItem[];
  createdAt: string;
  updatedAt: string | null;
}

export interface MaintenancePlanRequest {
  name: string;
  vehicleId: string | null;
  vehicleType: VehicleType | null;
  isActive: boolean;
  items: MaintenancePlanItemRequest[];
}

export interface MaintenanceScheduleItem {
  maintenancePlanItemId: string;
  serviceName: string;
  priority: MaintenancePriority;
  status: MaintenanceScheduleStatus;
  lastPerformedOn: string | null;
  lastPerformedKm: number | null;
  lastPerformedHours: number | null;
  nextDueOn: string | null;
  nextDueKm: number | null;
  nextDueHours: number | null;
}

export interface HourMeterReading {
  id: string;
  vehicleId: string;
  hours: number;
  readAt: string;
  source: 'Registration' | 'Manual' | 'WorkOrder' | 'Correction';
  status: HourMeterStatus;
  anomaly: string | null;
  notes: string | null;
  recordedByName: string | null;
  createdAt: string;
  reviewedAt: string | null;
  reviewedByName: string | null;
  reviewNotes: string | null;
}

export interface MaintenanceRequest {
  id: string;
  vehicleId: string;
  licensePlate: string;
  driverId: string | null;
  driverName: string | null;
  source: MaintenanceRequestSource;
  maintenanceType: MaintenanceType;
  priority: MaintenancePriority;
  description: string;
  reportedAt: string;
  odometerKm: number | null;
  hourMeter: number | null;
  occurrenceId: string | null;
  status: MaintenanceRequestStatus;
  reviewedAt: string | null;
  reviewedByName: string | null;
  rejectionReason: string | null;
  workOrderId: string | null;
  workOrderNumber: string | null;
  createdByName: string | null;
  createdAt: string;
}

export interface MaintenanceRequestRequest {
  vehicleId: string | null;
  driverId: string | null;
  source: MaintenanceRequestSource;
  maintenanceType: MaintenanceType;
  priority: MaintenancePriority;
  description: string;
  reportedAt: string | null;
  odometerKm: number | null;
  hourMeter: number | null;
  occurrenceId: string | null;
}

export interface WorkOrderItem {
  id: string;
  description: string;
  maintenancePlanItemId: string | null;
  isRequired: boolean;
  status: WorkOrderItemStatus;
  notes: string | null;
}

export type WorkOrderItemRequest = Omit<WorkOrderItem, 'id' | 'status'> & { id: string | null };

export interface WorkOrderPart {
  id: string;
  partName: string;
  partNumber: string | null;
  quantity: number;
  unitCost: number;
  totalCost: number;
  supplier: string | null;
  notes: string | null;
}

export interface WorkOrderLabor {
  id: string;
  technicianName: string;
  hours: number;
  hourlyRate: number;
  totalCost: number;
  description: string | null;
}

export interface WorkOrderListItem {
  id: string;
  number: string;
  vehicleId: string;
  licensePlate: string;
  type: MaintenanceType;
  priority: MaintenancePriority;
  status: WorkOrderStatus;
  openedAt: string;
  scheduledAt: string | null;
  description: string;
  totalCost: number | null;
}

export interface WorkOrder extends Omit<WorkOrderListItem, 'totalCost'> {
  implementId: string | null;
  implementPlate: string | null;
  maintenanceRequestId: string | null;
  workshopId: string | null;
  workshopName: string | null;
  nextStatuses: WorkOrderStatus[];
  startedAt: string | null;
  completedAt: string | null;
  odometerKm: number | null;
  hourMeter: number | null;
  diagnosis: string | null;
  resolution: string | null;
  notes: string | null;
  completedByName: string | null;
  partsCost: number | null;
  laborCost: number | null;
  otherCost: number | null;
  totalCost: number | null;
  downtimeMinutes: number | null;
  items: WorkOrderItem[];
  parts: WorkOrderPart[];
  labor: WorkOrderLabor[];
  createdByName: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface WorkOrderRequest {
  vehicleId: string | null;
  implementId: string | null;
  workshopId: string | null;
  type: MaintenanceType;
  priority: MaintenancePriority;
  scheduledAt: string | null;
  description: string;
  diagnosis: string | null;
  notes: string | null;
  items: WorkOrderItemRequest[];
}

export interface RepeatedProblem {
  description: string;
  occurrences: number;
}
