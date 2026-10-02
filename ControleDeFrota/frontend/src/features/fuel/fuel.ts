import type { StatusOption } from '../../components/common';
import type { FuelUnit } from '../../lib/fuel';
import { STATUS_COLOR } from '../../theme';
import type { FileInfo } from '../operations/api';
import type { Address } from '../maintenance/maintenance';
import type { VehicleFuelType, VehicleStatus } from '../vehicles/vehicles';

// Phase 4 enums → pt-BR labels and badge colors — the ONE place for these texts (fleet-development skill).

export type { FuelUnit };

export const FUEL_UNIT = {
  Liter: { label: 'Litro (L)' },
  CubicMeter: { label: 'Metro cúbico (m³)' },
  KilowattHour: { label: 'Quilowatt-hora (kWh)' },
};

export const FUEL_CATEGORY = {
  Diesel: { label: 'Diesel' },
  Gasoline: { label: 'Gasolina' },
  Ethanol: { label: 'Etanol' },
  Cng: { label: 'GNV' },
  Electric: { label: 'Elétrico' },
  Other: { label: 'Outro' },
};
export type FuelCategory = keyof typeof FUEL_CATEGORY;

/** Neutral wording on purpose: a review is a check, not an accusation (UX_UI.md). */
export const FUELING_STATUS = {
  Valid: { label: 'Válido', color: STATUS_COLOR.positive },
  PendingReview: { label: 'Requer revisão', color: STATUS_COLOR.warning },
  Cancelled: { label: 'Cancelado', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type FuelingStatus = keyof typeof FUELING_STATUS;

export const PAYMENT_METHOD = {
  FuelCard: { label: 'Cartão combustível' },
  Invoice: { label: 'Faturado' },
  Pix: { label: 'Pix' },
  DebitCard: { label: 'Cartão de débito' },
  CreditCard: { label: 'Cartão de crédito' },
  Cash: { label: 'Dinheiro' },
  InternalTank: { label: 'Tanque próprio' },
  Other: { label: 'Outro' },
};
export type PaymentMethod = keyof typeof PAYMENT_METHOD;

export const FUEL_ANOMALY_TYPE = {
  ExcessiveQuantity: { label: 'Quantidade acima do tanque' },
  AbnormalPrice: { label: 'Preço fora da média' },
  MileageJump: { label: 'Hodômetro em revisão' },
  HighFrequency: { label: 'Abastecimentos muito próximos' },
  FuelTypeMismatch: { label: 'Combustível diferente do cadastro' },
  LowConsumption: { label: 'Consumo abaixo do esperado' },
  HighConsumption: { label: 'Consumo acima do esperado' },
};
export type FuelAnomalyType = keyof typeof FUEL_ANOMALY_TYPE;

/** Why a fueling has (or not) a consumption figure — always explained, never a silent blank. */
export const CONSUMPTION_RESULT = {
  Calculated: { label: 'Calculado' },
  PartialFill: { label: 'Abastecimento parcial: entra no cálculo do próximo tanque cheio' },
  FirstFullTank: { label: 'Primeiro tanque cheio: o consumo aparece a partir do próximo' },
  NotReliable: { label: 'Não calculado: hodômetro corrigido ou em revisão, ou combustíveis de unidades diferentes no trecho' },
};
export type ConsumptionResult = keyof typeof CONSUMPTION_RESULT;

export const BASELINE_SOURCE = {
  Configured: { label: 'configurado no veículo' },
  VehicleHistory: { label: 'média do próprio veículo' },
  VehicleTypeAverage: { label: 'média dos veículos do mesmo tipo' },
};
export type BaselineSource = keyof typeof BASELINE_SOURCE;

export const HISTORY_GRANULARITY = {
  Day: { label: 'Por dia' },
  Week: { label: 'Por semana' },
  Month: { label: 'Por mês' },
};
export type HistoryGranularity = keyof typeof HISTORY_GRANULARITY;

export const COST_GROUPING = {
  Vehicle: { label: 'Por veículo' },
  Driver: { label: 'Por motorista' },
  Station: { label: 'Por posto' },
  FuelType: { label: 'Por combustível' },
};
export type CostGrouping = keyof typeof COST_GROUPING;

// ---------- catalog ----------

export interface FuelType {
  id: string;
  name: string;
  code: string;
  category: FuelCategory;
  unit: FuelUnit;
  isActive: boolean;
  description: string | null;
  fuelingCount: number;
}

export interface FuelStation {
  id: string;
  name: string;
  cnpj: string | null;
  address: Address;
  phone: string | null;
  contactName: string | null;
  isInternal: boolean;
  isActive: boolean;
  notes: string | null;
  fuelingCount: number;
  lastFueledOn: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface FuelPrice {
  id: string;
  fuelTypeId: string;
  fuelTypeName: string;
  unit: FuelUnit;
  price: number;
  effectiveFrom: string;
  notes: string | null;
  recordedByName: string | null;
  createdAt: string;
}

export interface FuelSettings {
  tankTolerancePercent: number;
  priceDeviationPercent: number;
  consumptionDeviationPercent: number;
  minHoursBetweenFuelings: number;
  requireDriver: boolean;
}

// ---------- fuelings ----------

export interface FuelingListItem {
  id: string;
  vehicleId: string;
  licensePlate: string;
  driverId: string | null;
  driverName: string | null;
  fuelStationId: string | null;
  fuelStationName: string | null;
  fuelTypeId: string;
  fuelTypeName: string;
  unit: FuelUnit;
  fueledAt: string;
  odometerKm: number;
  quantity: number;
  /** null when the caller may not see costs (and is not the author). */
  unitPrice: number | null;
  totalAmount: number | null;
  isFullTank: boolean;
  paymentMethod: PaymentMethod;
  status: FuelingStatus;
  consumptionResult: ConsumptionResult;
  consumption: number | null;
  anomalyCount: number;
}

export interface FuelingAnomaly {
  id: string;
  type: FuelAnomalyType;
  message: string;
  expectedValue: number | null;
  actualValue: number | null;
  detectedAt: string;
  reviewedAt: string | null;
  reviewedByName: string | null;
}

export interface FuelingChange {
  field: string;
  label: string;
  from: string | null;
  to: string | null;
}

export interface FuelingCorrection {
  id: string;
  correctedAt: string;
  correctedByName: string | null;
  reason: string;
  changes: FuelingChange[];
}

export interface Fueling extends Omit<FuelingListItem, 'anomalyCount'> {
  receiptNumber: string | null;
  notes: string | null;
  canSeeCosts: boolean;
  segmentDistanceKm: number | null;
  segmentQuantity: number | null;
  segmentCost: number | null;
  expectedConsumption: number | null;
  baselineSource: BaselineSource | null;
  consumptionDeviationPercent: number | null;
  odometerReadingStatus: 'Valid' | 'PendingReview' | 'Rejected' | null;
  reviewedAt: string | null;
  reviewedByName: string | null;
  reviewNotes: string | null;
  cancelledAt: string | null;
  cancelledByName: string | null;
  cancellationReason: string | null;
  createdByName: string | null;
  createdAt: string;
  anomalies: FuelingAnomaly[];
  corrections: FuelingCorrection[];
  files: FileInfo[];
  /** Decided by the API (permissions + workflow) — the UI never re-implements the rules. */
  actions: { canCorrect: boolean; canCancel: boolean; canReview: boolean };
}

export interface FuelingFields {
  driverId: string | null;
  fuelStationId: string | null;
  fuelTypeId: string | null;
  fueledAt: string | null;
  odometerKm: number | null;
  quantity: number | null;
  unitPrice: number | null;
  totalAmount: number | null;
  isFullTank: boolean;
  paymentMethod: PaymentMethod | null;
  receiptNumber: string | null;
  notes: string | null;
}

export interface FuelingRequest extends FuelingFields {
  vehicleId: string | null;
  fileIds: string[];
}

export interface FuelingCorrectionRequest extends FuelingFields {
  reason: string;
}

export interface FuelingFormDefaults {
  vehicleId: string;
  licensePlate: string;
  vehicleFuelType: VehicleFuelType;
  vehicleStatus: VehicleStatus;
  currentOdometerKm: number;
  odometerUpdatedAt: string | null;
  tankCapacity: number | null;
  currentDriverId: string | null;
  currentDriverName: string | null;
  lastFuelTypeId: string | null;
  lastFuelStationId: string | null;
  lastFueledAt: string | null;
  lastFuelingOdometerKm: number | null;
  requireDriver: boolean;
}

export interface FuelPriceHint {
  price: number;
  date: string;
  source: 'Reference' | 'LastFueling';
}

// ---------- analytics ----------

export interface FuelTotals {
  fuelings: number;
  liters: number;
  otherUnitsQuantity: number;
  totalCost: number | null;
  averagePricePerLiter: number | null;
  averageConsumption: number | null;
  measuredDistanceKm: number;
  costPerKm: number | null;
}

export interface FuelHistoryPoint {
  start: string;
  end: string;
  label: string;
  fuelings: number;
  quantity: number;
  cost: number | null;
  distanceKm: number;
  consumption: number | null;
}

export interface VehicleFuelMetric {
  vehicleId: string;
  licensePlate: string;
  fuelings: number;
  quantity: number;
  cost: number | null;
  distanceKm: number;
  consumption: number | null;
  expectedConsumption: number | null;
  variancePercent: number | null;
  costPerKm: number | null;
}

export interface FuelDashboard {
  from: string;
  to: string;
  canSeeCosts: boolean;
  totals: FuelTotals;
  pendingReview: number;
  consumptionAnomalies: number;
  attention: { fuelingId: string; vehicleId: string; licensePlate: string; fueledAt: string; anomalyTypes: FuelAnomalyType[]; message: string }[];
  highestCostVehicles: VehicleFuelMetric[];
  lowestConsumptionVehicles: VehicleFuelMetric[];
  recent: FuelingListItem[];
  monthly: FuelHistoryPoint[];
  byFuelType: { fuelTypeId: string; name: string; unit: FuelUnit; fuelings: number; quantity: number; cost: number | null }[];
}

export interface VehicleFuel {
  vehicleId: string;
  from: string;
  to: string;
  canSeeCosts: boolean;
  unit: FuelUnit;
  totals: FuelTotals;
  expectedConsumption: number | null;
  baselineSource: BaselineSource | null;
  tankCapacity: number | null;
  pendingReview: number;
  lastFueling: FuelingListItem | null;
  granularity: HistoryGranularity;
  history: FuelHistoryPoint[];
}

export interface ReportPage<T> {
  from: string;
  to: string;
  canSeeCosts: boolean;
  rows: { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };
}

export interface ConsumptionReportRow {
  vehicleId: string;
  licensePlate: string;
  unit: FuelUnit;
  segments: number;
  distanceKm: number;
  quantity: number;
  consumption: number | null;
  expectedConsumption: number | null;
  variancePercent: number | null;
}

export interface CostReportRow {
  key: string | null;
  name: string;
  fuelings: number;
  liters: number;
  cost: number;
  distanceKm: number | null;
  costPerKm: number | null;
}

export interface StationReportRow {
  fuelStationId: string | null;
  stationName: string;
  fuelTypeId: string;
  fuelTypeName: string;
  unit: FuelUnit;
  fuelings: number;
  quantity: number;
  averagePrice: number | null;
  minPrice: number | null;
  maxPrice: number | null;
  totalCost: number | null;
}

export interface PriceReportRow {
  year: number;
  month: number;
  fuelTypeId: string;
  fuelTypeName: string;
  unit: FuelUnit;
  fuelings: number;
  averagePrice: number;
  minPrice: number;
  maxPrice: number;
}
