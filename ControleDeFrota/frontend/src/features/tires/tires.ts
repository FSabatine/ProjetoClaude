import type { StatusOption } from '../../components/common';
import { STATUS_COLOR } from '../../theme';
import type { FileInfo } from '../operations/api';

// Phase 5 enums → pt-BR labels and badge colors — the ONE place for these texts (fleet-development skill).

export const TIRE_STATUS = {
  InStock: { label: 'Em estoque', color: STATUS_COLOR.positive },
  Installed: { label: 'Instalado', color: STATUS_COLOR.active },
  UnderInspection: { label: 'Em avaliação', color: STATUS_COLOR.warning },
  UnderRepair: { label: 'Em conserto', color: STATUS_COLOR.warning },
  UnderRetread: { label: 'Em recapagem', color: STATUS_COLOR.warning },
  Disposed: { label: 'Baixado', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type TireStatus = keyof typeof TIRE_STATUS;

export const TIRE_APPLICATION = {
  AllPosition: { label: 'Uso misto (qualquer posição)' },
  Steer: { label: 'Direcional' },
  Drive: { label: 'Tração' },
  Trailer: { label: 'Reboque / livre' },
};
export type TireApplication = keyof typeof TIRE_APPLICATION;

export const TIRE_CONSTRUCTION = { Radial: { label: 'Radial' }, Bias: { label: 'Diagonal (convencional)' } };
export type TireConstruction = keyof typeof TIRE_CONSTRUCTION;

export const AXLE_TYPE = {
  Steer: { label: 'Direcional' },
  Drive: { label: 'Tração' },
  Free: { label: 'Livre' },
  Trailer: { label: 'Implemento' },
};
export type AxleType = keyof typeof AXLE_TYPE;

export const LAYOUT_TARGET = { Vehicle: { label: 'Veículo' }, Implement: { label: 'Implemento' } };
export type LayoutTarget = keyof typeof LAYOUT_TARGET;

export const REMOVAL_REASON = {
  Inspection: { label: 'Inspeção / avaliação' },
  Damage: { label: 'Dano' },
  Replacement: { label: 'Substituição' },
  Retread: { label: 'Recapagem' },
  Repair: { label: 'Conserto' },
  EndOfLife: { label: 'Fim de vida' },
  Rotation: { label: 'Rodízio' },
  VehicleSale: { label: 'Venda do veículo' },
  VehicleDecommission: { label: 'Baixa do veículo' },
  Transfer: { label: 'Transferência' },
  Other: { label: 'Outro' },
};
export type RemovalReason = keyof typeof REMOVAL_REASON;

/** Where the removed tire goes (decides its next status). "Installed" = rotation/transfer, not offered in the removal form. */
export const REMOVAL_DESTINATION = {
  Stock: { label: 'Estoque (pode ser reutilizado)' },
  Evaluation: { label: 'Avaliação (decidir depois)' },
  Repair: { label: 'Conserto' },
  Retread: { label: 'Recapagem' },
  Disposal: { label: 'Baixa (fim de vida)' },
  Installed: { label: 'Outra posição' },
};
export type RemovalDestination = keyof typeof REMOVAL_DESTINATION;

export const INSTALL_REASON = {
  Installation: { label: 'Instalação' },
  Replacement: { label: 'Substituição' },
  Rotation: { label: 'Rodízio' },
  Transfer: { label: 'Transferência' },
};
export type InstallReason = keyof typeof INSTALL_REASON;

export const DISPOSAL_REASON = {
  EndOfLife: { label: 'Fim de vida' },
  IrreparableDamage: { label: 'Dano irreparável' },
  SafetyConcern: { label: 'Segurança' },
  Lost: { label: 'Extravio' },
  Sold: { label: 'Venda' },
  Transferred: { label: 'Transferência para outra empresa' },
  Other: { label: 'Outro' },
};
export type DisposalReason = keyof typeof DISPOSAL_REASON;

/** Observations, never a diagnosis (seção 20). */
export const WEAR_PATTERN = {
  Normal: { label: 'Normal' },
  CenterWear: { label: 'Desgaste no centro' },
  ShoulderWear: { label: 'Desgaste nos ombros' },
  OneSidedWear: { label: 'Desgaste de um lado só' },
  IrregularWear: { label: 'Desgaste irregular' },
  Cupping: { label: 'Escamação (cupping)' },
  Unknown: { label: 'Não avaliado' },
};
export type WearPattern = keyof typeof WEAR_PATTERN;

export const DAMAGE_TYPE = {
  Puncture: { label: 'Furo' },
  Cut: { label: 'Corte' },
  Crack: { label: 'Trinca' },
  Bulge: { label: 'Bolha' },
  SidewallDamage: { label: 'Dano na lateral' },
  BeadDamage: { label: 'Dano no talão' },
  Other: { label: 'Outro' },
};
export type DamageType = keyof typeof DAMAGE_TYPE;

export const TIRE_CONDITION = {
  Good: { label: 'Bom', color: STATUS_COLOR.positive },
  Attention: { label: 'Atenção', color: STATUS_COLOR.warning },
  Unfit: { label: 'Impróprio para uso', color: STATUS_COLOR.danger },
} satisfies Record<string, StatusOption>;
export type TireCondition = keyof typeof TIRE_CONDITION;

export const PRESSURE_UNIT = { Psi: { label: 'psi' }, Bar: { label: 'bar' }, Kpa: { label: 'kPa' } };
export type PressureUnit = keyof typeof PRESSURE_UNIT;

export const PRESSURE_CHECK = {
  WithinRange: { label: 'Dentro da referência', color: STATUS_COLOR.positive },
  Low: { label: 'Abaixo da referência', color: STATUS_COLOR.warning },
  High: { label: 'Acima da referência', color: STATUS_COLOR.warning },
  NotEvaluated: { label: 'Sem referência', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type PressureCheck = keyof typeof PRESSURE_CHECK;

export const INSPECTION_SOURCE = {
  Inspection: { label: 'Inspeção' },
  Removal: { label: 'Medição na remoção' },
  Retread: { label: 'Banda nova (recapagem)' },
  Registration: { label: 'Cadastro' },
};
export type InspectionSource = keyof typeof INSPECTION_SOURCE;

export const SERVICE_KIND = { Repair: { label: 'Conserto' }, Retread: { label: 'Recapagem' } };
export type ServiceKind = keyof typeof SERVICE_KIND;

export const SERVICE_STATUS = {
  Open: { label: 'No fornecedor', color: STATUS_COLOR.warning },
  Completed: { label: 'Concluído', color: STATUS_COLOR.positive },
  Cancelled: { label: 'Cancelado', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type ServiceStatus = keyof typeof SERVICE_STATUS;

export const SERVICE_RESULT = { Approved: { label: 'Aprovado' }, Rejected: { label: 'Reprovado' } };
export type ServiceResult = keyof typeof SERVICE_RESULT;

export const REPAIR_TYPE = {
  Puncture: { label: 'Furo' },
  Vulcanization: { label: 'Vulcanização' },
  BeadRepair: { label: 'Talão' },
  SidewallRepair: { label: 'Lateral' },
  Other: { label: 'Outro' },
};
export type RepairType = keyof typeof REPAIR_TYPE;

export const COST_TYPE = {
  Repair: { label: 'Conserto' },
  Retread: { label: 'Recapagem' },
  Installation: { label: 'Montagem / balanceamento' },
  Other: { label: 'Outro' },
};
export type CostType = keyof typeof COST_TYPE;

/** Neutral: "requires review" — never a cause (seção 35). */
export const ANOMALY_TYPE = {
  RapidTreadLoss: { label: 'Perda de sulco acima da referência' },
  RepeatedRepairs: { label: 'Consertos repetidos' },
  RepeatedPunctures: { label: 'Furos repetidos' },
  ShortLifecycle: { label: 'Vida curta' },
  RecurringPositionDamage: { label: 'Danos repetidos na mesma posição' },
};
export type AnomalyType = keyof typeof ANOMALY_TYPE;

export const ALERT_TYPE = {
  TreadBelowMinimum: { label: 'Sulco no mínimo' },
  TreadNearMinimum: { label: 'Sulco perto do mínimo' },
  InspectionOverdue: { label: 'Inspeção atrasada' },
  UnevenWear: { label: 'Desgaste irregular' },
  DamageReported: { label: 'Dano registrado' },
  AgeExceeded: { label: 'Idade acima do limite' },
  PressureOutOfRange: { label: 'Pressão fora da referência' },
};
export type AlertType = keyof typeof ALERT_TYPE;

export const INSPECTION_FILTER = { Overdue: { label: 'Atrasada' }, UpToDate: { label: 'Em dia' }, Never: { label: 'Nunca inspecionado' } };
export const ALERT_FILTER = {
  NearReplacement: { label: 'Perto da substituição' },
  BelowMinimum: { label: 'No sulco mínimo' },
  InspectionOverdue: { label: 'Inspeção atrasada' },
  DamageOrUnevenWear: { label: 'Dano ou desgaste irregular' },
  AgeExceeded: { label: 'Idade acima do limite' },
  RequiresReview: { label: 'Requer revisão' },
};

export type TireSide = 'Left' | 'Right' | 'None';
export type TirePlacement = 'Single' | 'Outer' | 'Inner' | 'Spare';

// ---------- API types ----------

export interface TireModel {
  id: string;
  brand: string;
  name: string;
  size: string;
  application: TireApplication;
  construction: TireConstruction;
  loadIndex: string | null;
  speedRating: string | null;
  originalTreadDepthMm: number | null;
  isActive: boolean;
  notes: string | null;
  tireCount: number;
}

export interface LayoutAxle {
  number: number;
  type: AxleType;
  isDual: boolean;
  isRequired: boolean;
  allowedSize: string | null;
  recommendedPressurePsi: number | null;
}

export interface TirePosition {
  code: string;
  label: string;
  axleNumber: number;
  axleType: AxleType | null;
  side: TireSide;
  placement: TirePlacement;
  isRequired: boolean;
  allowedSize: string | null;
  recommendedPressurePsi: number | null;
}

export interface TireLayout {
  id: string;
  name: string;
  target: LayoutTarget;
  description: string | null;
  spareCount: number;
  isActive: boolean;
  axles: LayoutAxle[];
  positions: TirePosition[];
  assetCount: number;
  tirePositionCount: number;
}

export interface TireSettings {
  minTreadDepthMm: number;
  treadWarningDepthMm: number;
  inspectionIntervalDays: number;
  maxAgeYears: number;
  pressureTolerancePercent: number;
  pressureUnit: PressureUnit;
  rapidWearMmPer1000Km: number;
  minExpectedLifeKm: number;
  autoMaintenanceRequestOnUnfit: boolean;
}

export interface TireAlert {
  type: AlertType;
  severity: 'Warning' | 'Critical';
  message: string;
}

export interface TireLocation {
  installationId: string;
  vehicleId: string | null;
  implementId: string | null;
  assetLabel: string;
  positionCode: string;
  positionLabel: string;
  isSpare: boolean;
  installedAt: string;
  installedOdometerKm: number | null;
}

export interface TireListItem {
  id: string;
  code: string;
  tireModelId: string;
  brand: string;
  modelName: string;
  size: string;
  status: TireStatus;
  serialNumber: string | null;
  dot: string | null;
  manufacturedOn: string | null;
  location: TireLocation | null;
  storageLocation: string | null;
  currentTreadDepthMm: number | null;
  currentKm: number | null;
  retreadCount: number;
  lastInspectedAt: string | null;
  alertCount: number;
  topAlertSeverity: 'Warning' | 'Critical' | null;
  openAnomalyCount: number;
}

export interface TireCostSummary {
  purchase: number | null;
  repairs: number;
  retreads: number;
  installation: number;
  other: number;
  total: number;
  km: number | null;
  costPerKm: number | null;
  costPerKmNote: string | null;
}

export interface TireAnomaly {
  id: string;
  type: AnomalyType;
  message: string;
  detectedAt: string;
  reviewedAt: string | null;
  reviewedByName: string | null;
  reviewNotes: string | null;
}

export interface TireActions {
  canEdit: boolean;
  canDelete: boolean;
  canInstall: boolean;
  canRemove: boolean;
  canReplace: boolean;
  canTransfer: boolean;
  canInspect: boolean;
  canSendToRepair: boolean;
  canSendToRetread: boolean;
  canRepairInPlace: boolean;
  canSendToEvaluation: boolean;
  canReturnToStock: boolean;
  canDispose: boolean;
  canManageService: boolean;
  canAddCost: boolean;
  canReviewAnomalies: boolean;
  canCorrectHistory: boolean;
}

export interface Tire {
  id: string;
  code: string;
  tireModelId: string;
  brand: string;
  modelName: string;
  size: string;
  application: TireApplication;
  construction: TireConstruction;
  loadIndex: string | null;
  speedRating: string | null;
  serialNumber: string | null;
  dot: string | null;
  manufacturedOn: string | null;
  purchasedOn: string | null;
  purchasePrice: number | null;
  supplier: string | null;
  originalTreadDepthMm: number | null;
  storageLocation: string | null;
  notes: string | null;
  status: TireStatus;
  location: TireLocation | null;
  openServiceOrder: { id: string; kind: ServiceKind; sentAt: string; providerName: string | null; retreadNumber: number | null } | null;
  currentKm: number | null;
  accumulatedKm: number;
  hasUnmeasuredDistance: boolean;
  retreadCount: number;
  repairCount: number;
  currentTreadDepthMm: number | null;
  treadMeasuredAt: string | null;
  treadUsedPercent: number | null;
  lastInspectedAt: string | null;
  nextInspectionDue: string | null;
  canSeeCosts: boolean;
  costs: TireCostSummary | null;
  alerts: TireAlert[];
  anomalies: TireAnomaly[];
  disposedAt: string | null;
  disposalReason: DisposalReason | null;
  disposalDestination: string | null;
  disposalNotes: string | null;
  disposedByName: string | null;
  files: FileInfo[];
  actions: TireActions;
  createdByName: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface TireRequest {
  code: string | null;
  tireModelId: string | null;
  serialNumber: string | null;
  dot: string | null;
  manufacturedOn: string | null;
  purchasedOn: string | null;
  purchasePrice: number | null;
  supplier: string | null;
  originalTreadDepthMm: number | null;
  currentTreadDepthMm: number | null;
  retreadCount: number;
  storageLocation: string | null;
  notes: string | null;
  fileIds?: string[];
}

export interface TireInstallation {
  id: string;
  vehicleId: string | null;
  implementId: string | null;
  assetLabel: string;
  positionCode: string;
  positionLabel: string;
  isSpare: boolean;
  installedAt: string;
  installedOdometerKm: number | null;
  installReason: InstallReason;
  installedByName: string | null;
  removedAt: string | null;
  removedOdometerKm: number | null;
  removalReason: RemovalReason | null;
  removalDestination: RemovalDestination | null;
  removedByName: string | null;
  distanceKm: number | null;
  isRotation: boolean;
  notes: string | null;
  removalNotes: string | null;
}

export interface TireInspection {
  id: string;
  tireId: string;
  tireCode: string;
  inspectedAt: string;
  source: InspectionSource;
  vehicleId: string | null;
  implementId: string | null;
  assetLabel: string | null;
  positionCode: string | null;
  positionLabel: string | null;
  odometerKm: number | null;
  tireKm: number | null;
  treadDepthMm: number | null;
  pressure: number | null;
  pressureUnit: PressureUnit | null;
  pressureCheck: PressureCheck | null;
  condition: TireCondition;
  wearPattern: WearPattern;
  damages: DamageType[];
  notes: string | null;
  requiresAction: boolean;
  occurrenceId: string | null;
  maintenanceRequestId: string | null;
  inspectedByName: string | null;
  files: FileInfo[];
}

export interface TireServiceOrder {
  id: string;
  tireId: string;
  tireCode: string;
  kind: ServiceKind;
  status: ServiceStatus;
  result: ServiceResult | null;
  inPlace: boolean;
  workshopId: string | null;
  providerName: string | null;
  sentAt: string;
  completedAt: string | null;
  repairType: RepairType | null;
  retreadNumber: number | null;
  treadPattern: string | null;
  newTreadDepthMm: number | null;
  cost: number | null;
  warrantyUntil: string | null;
  description: string | null;
  resultNotes: string | null;
  cancellationReason: string | null;
  createdByName: string | null;
  files: FileInfo[];
  canManage: boolean;
}

export interface TireCost {
  id: string;
  type: CostType;
  incurredOn: string;
  amount: number;
  description: string | null;
  serviceOrderId: string | null;
  canDelete: boolean;
  createdByName: string | null;
}

export interface AssetInstalledTire {
  tireId: string;
  code: string;
  brand: string;
  modelName: string;
  size: string;
  status: TireStatus;
  installationId: string;
  installedAt: string;
  installedOdometerKm: number | null;
  currentKm: number | null;
  kmInPosition: number | null;
  currentTreadDepthMm: number | null;
  treadMeasuredAt: string | null;
  originalTreadDepthMm: number | null;
  lastInspectedAt: string | null;
  retreadCount: number;
  alerts: TireAlert[];
  openAnomalyCount: number;
  compatibilityWarnings: string[];
}

export interface AssetTirePosition {
  position: TirePosition;
  tire: AssetInstalledTire | null;
}

export interface AssetTires {
  vehicleId: string | null;
  implementId: string | null;
  assetLabel: string;
  isInactive: boolean;
  currentOdometerKm: number | null;
  odometerUpdatedAt: string | null;
  layoutId: string | null;
  layoutName: string | null;
  axles: LayoutAxle[];
  positions: AssetTirePosition[];
  installedCount: number;
  emptyRequiredCount: number;
  alertCount: number;
  recentMovements: { id: number; type: string; occurredAt: string; summary: string; tireId: string | null }[];
  actions: { canInstall: boolean; canRemove: boolean; canReplace: boolean; canRotate: boolean; canInspect: boolean; canChangeLayout: boolean };
}

export interface TireCompatibility {
  status: 'Compatible' | 'Warning' | 'Incompatible' | 'NotVerified';
  messages: string[];
  positionOccupied: boolean;
  occupiedBy: string | null;
}

export interface TireDashboard {
  status: { total: number; installed: number; inStock: number; underInspection: number; underRepair: number; underRetread: number; disposed: number };
  attention: { nearReplacement: number; belowMinimum: number; inspectionOverdue: number; damageOrUnevenWear: number; requiresReview: number; ageExceeded: number };
  abnormalWear: { tireId: string; code: string; brand: string; modelName: string; size: string; status: TireStatus; assetLabel: string | null; positionLabel: string | null; currentTreadDepthMm: number | null; detail: string }[];
  highestCostPerKm: { tireId: string; code: string; brand: string; modelName: string; size: string; totalCost: number; km: number; costPerKm: number }[] | null;
  recentMovements: { id: number; type: string; occurredAt: string; summary: string; tireId: string | null }[];
  upcomingInspections: { tireId: string; code: string; assetLabel: string | null; positionLabel: string | null; dueAt: string; overdue: boolean }[];
  canSeeCosts: boolean;
  minTreadDepthMm: number;
  treadWarningDepthMm: number;
  inspectionIntervalDays: number;
}

export interface TireReportPage<T> {
  from: string | null;
  to: string | null;
  canSeeCosts: boolean;
  rows: { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };
}

export interface LifecycleReportRow {
  installationId: string;
  tireId: string;
  code: string;
  brand: string;
  modelName: string;
  size: string;
  assetLabel: string;
  positionLabel: string;
  installedAt: string;
  removedAt: string | null;
  removalReason: RemovalReason | null;
  distanceKm: number | null;
  tireKm: number;
  repairCount: number;
  retreadCount: number;
  lifecycleCost: number | null;
  costPerKm: number | null;
}

export interface InspectionReportRow {
  inspectionId: string;
  tireId: string;
  code: string;
  assetLabel: string | null;
  positionLabel: string | null;
  inspectedAt: string;
  source: InspectionSource;
  treadDepthMm: number | null;
  pressure: number | null;
  pressureUnit: PressureUnit | null;
  pressureCheck: PressureCheck | null;
  condition: TireCondition;
  wearPattern: WearPattern;
  damageCount: number;
}

export interface CostReportRow {
  tireId: string;
  code: string;
  brand: string;
  modelName: string;
  size: string;
  status: TireStatus;
  purchase: number | null;
  repairs: number;
  retreads: number;
  other: number;
  total: number;
  km: number;
  hasUnmeasuredDistance: boolean;
  costPerKm: number | null;
}
