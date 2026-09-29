import { useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import { createResource } from '../../api/crud';
import type { Address } from '../drivers/drivers';

export interface CompanyListItem {
  id: string;
  legalName: string;
  tradeName: string | null;
  cnpj: string;
  city: string | null;
  state: string | null;
  isActive: boolean;
  userCount: number;
}

export interface Company {
  id: string;
  legalName: string;
  tradeName: string | null;
  cnpj: string;
  stateRegistration: string | null;
  email: string | null;
  phone: string | null;
  address: Address;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface CompanyRequest {
  legalName: string;
  tradeName: string | null;
  cnpj: string;
  stateRegistration: string | null;
  email: string | null;
  phone: string | null;
  address: Record<keyof Address, string | null>;
  isActive: boolean;
}

export const companiesApi = createResource<CompanyListItem, Company, CompanyRequest>('/companies');

export const useCurrentCompany = () =>
  useQuery({ queryKey: ['/companies', 'current'], queryFn: () => api.get<Company>('/companies/current').then((r) => r.data) });
