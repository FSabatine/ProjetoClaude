import { useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import { createResource } from '../../api/crud';
import type { StatusOption } from '../../components/common';
import { STATUS_COLOR } from '../../theme';

export const USER_STATUS = {
  Active: { label: 'Ativo', color: STATUS_COLOR.positive },
  Inactive: { label: 'Inativo', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type UserStatus = keyof typeof USER_STATUS;

export interface UserListItem {
  id: string;
  name: string;
  email: string;
  status: UserStatus;
  companyId: string;
  companyName: string;
  roles: string[];
  lastLoginAt: string | null;
  isLockedOut: boolean;
}

export interface User {
  id: string;
  name: string;
  email: string;
  status: UserStatus;
  companyId: string;
  companyName: string;
  roles: { id: number; key: string; name: string }[];
  lastLoginAt: string | null;
  lockoutEndAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface UserRequest {
  name: string;
  email: string;
  password?: string;
  companyId?: string | null;
  status: UserStatus;
  roleIds: number[];
}

export interface Role {
  id: number;
  key: string;
  name: string;
  description: string;
  isSystem: boolean;
  permissions: string[];
  isAssignable: boolean;
}

export interface PermissionInfo {
  id: number;
  key: string;
  module: string;
  description: string;
}

export const usersApi = createResource<UserListItem, User, UserRequest>('/users');

export const useRoles = () => useQuery({ queryKey: ['roles'], queryFn: () => api.get<Role[]>('/roles').then((r) => r.data), staleTime: 5 * 60_000 });

export const usePermissionCatalog = () =>
  useQuery({ queryKey: ['permissions'], queryFn: () => api.get<PermissionInfo[]>('/permissions').then((r) => r.data), staleTime: 5 * 60_000 });

export const resetPassword = (id: string, newPassword: string) => api.post(`/users/${id}/reset-password`, { newPassword });
