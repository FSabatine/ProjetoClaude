import { createResource } from '../../api/crud';
import type { StatusOption } from '../../components/common';
import { STATUS_COLOR } from '../../theme';

export const IMPLEMENT_STATUS = {
  Available: { label: 'Disponível', color: STATUS_COLOR.positive },
  InUse: { label: 'Em uso', color: STATUS_COLOR.active },
  UnderMaintenance: { label: 'Em manutenção', color: STATUS_COLOR.warning },
  Inactive: { label: 'Inativo', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type ImplementStatus = keyof typeof IMPLEMENT_STATUS;

export const IMPLEMENT_TYPE = {
  Trailer: { label: 'Reboque' },
  SemiTrailer: { label: 'Semirreboque' },
  Tanker: { label: 'Tanque' },
  BoxBody: { label: 'Baú' },
  Sider: { label: 'Sider' },
  Dolly: { label: 'Dolly' },
  Other: { label: 'Outro' },
};
export type ImplementType = keyof typeof IMPLEMENT_TYPE;

export const CAPACITY_UNIT = {
  Kg: { label: 'kg', long: 'Quilogramas (kg)' },
  Liters: { label: 'L', long: 'Litros (L)' },
  CubicMeters: { label: 'm³', long: 'Metros cúbicos (m³)' },
};
export type CapacityUnit = keyof typeof CAPACITY_UNIT;

export interface ImplementListItem {
  id: string;
  licensePlate: string;
  manufacturer: string;
  model: string;
  modelYear: number;
  type: ImplementType;
  capacity: number | null;
  capacityUnit: CapacityUnit | null;
  status: ImplementStatus;
}

export interface Implement extends ImplementListItem {
  renavam: string;
  chassis: string;
  manufacturingYear: number;
  tareWeightKg: number | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ImplementRequest {
  licensePlate: string;
  renavam: string;
  chassis: string;
  manufacturer: string;
  model: string;
  manufacturingYear: number | null;
  modelYear: number | null;
  type: ImplementType | null;
  capacity: number | null;
  capacityUnit: CapacityUnit | null;
  tareWeightKg: number | null;
  status: ImplementStatus;
  notes: string | null;
}

export const implementsApi = createResource<ImplementListItem, Implement, ImplementRequest>('/implements');
