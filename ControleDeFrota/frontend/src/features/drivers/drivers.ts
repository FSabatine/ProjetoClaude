import { createResource } from '../../api/crud';
import type { StatusOption } from '../../components/common';
import { STATUS_COLOR } from '../../theme';

export const DRIVER_STATUS = {
  Active: { label: 'Ativo', color: STATUS_COLOR.positive },
  OnLeave: { label: 'Afastado', color: STATUS_COLOR.warning },
  Inactive: { label: 'Desligado', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type DriverStatus = keyof typeof DRIVER_STATUS;

export const LICENSE_STATE = {
  Valid: { label: 'CNH válida', color: STATUS_COLOR.positive },
  ExpiringSoon: { label: 'CNH vencendo', color: STATUS_COLOR.warning },
  Expired: { label: 'CNH vencida', color: STATUS_COLOR.danger },
} satisfies Record<string, StatusOption>;
export type LicenseState = keyof typeof LICENSE_STATE;

export const LICENSE_ALERT_FILTER = {
  Expired: { label: 'CNH vencida' },
  ExpiringSoon: { label: 'CNH vencendo (30 dias)' },
};

export const LICENSE_CATEGORIES = ['A', 'B', 'C', 'D', 'E', 'AB', 'AC', 'AD', 'AE'] as const;
export type LicenseCategory = (typeof LICENSE_CATEGORIES)[number];

export interface Address {
  street: string | null;
  number: string | null;
  complement: string | null;
  neighborhood: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
}

export interface DriverListItem {
  id: string;
  fullName: string;
  cpf: string;
  phone: string | null;
  licenseCategory: LicenseCategory;
  licenseExpiresOn: string;
  licenseState: LicenseState;
  status: DriverStatus;
}

export interface Driver extends DriverListItem {
  rg: string | null;
  birthDate: string;
  email: string | null;
  address: Address;
  licenseNumber: string;
  performsPaidActivity: boolean;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface DriverRequest {
  fullName: string;
  cpf: string;
  rg: string | null;
  birthDate: string | null;
  phone: string | null;
  email: string | null;
  address: Record<keyof Address, string | null>;
  licenseNumber: string;
  licenseCategory: LicenseCategory | null;
  licenseExpiresOn: string | null;
  performsPaidActivity: boolean;
  status: DriverStatus;
  notes: string | null;
}

export const driversApi = createResource<DriverListItem, Driver, DriverRequest>('/drivers');
