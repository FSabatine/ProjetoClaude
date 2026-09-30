import { createResource } from '../../api/crud';
import type { StatusOption } from '../../components/common';
import { STATUS_COLOR } from '../../theme';
import type { VehicleOperationalStatus } from '../operations/labels';

// Labels and colors: the ONE place mapping enum values to pt-BR text (DEVELOPMENT_GUIDELINES.md).
export const VEHICLE_STATUS = {
  Available: { label: 'Disponível', color: STATUS_COLOR.positive },
  OnTrip: { label: 'Em viagem', color: STATUS_COLOR.active },
  UnderMaintenance: { label: 'Em manutenção', color: STATUS_COLOR.warning },
  Unavailable: { label: 'Indisponível', color: STATUS_COLOR.danger },
  Inactive: { label: 'Inativo', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type VehicleStatus = keyof typeof VEHICLE_STATUS;

export const VEHICLE_TYPE = {
  Truck: { label: 'Caminhão' },
  TruckTractor: { label: 'Cavalo mecânico' },
  Van: { label: 'Van' },
  Pickup: { label: 'Picape / utilitário' },
  Car: { label: 'Automóvel' },
  Motorcycle: { label: 'Motocicleta' },
  Bus: { label: 'Ônibus' },
  Other: { label: 'Outro' },
};
export type VehicleType = keyof typeof VEHICLE_TYPE;

export const VEHICLE_CATEGORY = {
  Light: { label: 'Leve' },
  Medium: { label: 'Médio' },
  SemiHeavy: { label: 'Semipesado' },
  Heavy: { label: 'Pesado' },
};
export type VehicleCategory = keyof typeof VEHICLE_CATEGORY;

export const FUEL_TYPE = {
  DieselS10: { label: 'Diesel S10' },
  DieselS500: { label: 'Diesel S500' },
  Gasoline: { label: 'Gasolina' },
  Ethanol: { label: 'Etanol' },
  Flex: { label: 'Flex' },
  Cng: { label: 'GNV' },
  Electric: { label: 'Elétrico' },
  Hybrid: { label: 'Híbrido' },
  Other: { label: 'Outro' },
};
export type FuelType = keyof typeof FUEL_TYPE;

export interface VehicleListItem {
  id: string;
  licensePlate: string;
  manufacturer: string;
  model: string;
  modelYear: number;
  type: VehicleType;
  currentOdometerKm: number;
  odometerUpdatedAt: string | null;
  status: VehicleStatus;
  operationalStatus: VehicleOperationalStatus;
  currentDriverId: string | null;
  currentDriverName: string | null;
}

export interface CurrentAssignment {
  id: string;
  driverId: string;
  driverName: string;
  startedAt: string;
}

export interface Vehicle extends Omit<VehicleListItem, 'currentDriverId' | 'currentDriverName'> {
  currentAssignment: CurrentAssignment | null;
  renavam: string;
  chassis: string;
  manufacturingYear: number;
  color: string | null;
  category: VehicleCategory | null;
  fuelType: FuelType;
  cargoCapacityKg: number | null;
  tareWeightKg: number | null;
  hourMeter: number | null;
  acquisitionDate: string | null;
  acquisitionValue: number | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface VehicleRequest {
  licensePlate: string;
  renavam: string;
  chassis: string;
  manufacturer: string;
  model: string;
  manufacturingYear: number | null;
  modelYear: number | null;
  color: string | null;
  type: VehicleType | null;
  category: VehicleCategory | null;
  fuelType: FuelType | null;
  cargoCapacityKg: number | null;
  tareWeightKg: number | null;
  currentOdometerKm: number | null;
  hourMeter: number | null;
  status: VehicleStatus;
  acquisitionDate: string | null;
  acquisitionValue: number | null;
  notes: string | null;
}

export const vehiclesApi = createResource<VehicleListItem, Vehicle, VehicleRequest>('/vehicles');
