/** Mirror of Fleet.Domain.Authorization.Permissions. Hiding UI is convenience — the API enforces access. */
export const PERMISSIONS = {
  dashboard: { view: 'dashboard.view' },
  companies: { view: 'companies.view', update: 'companies.update', manage: 'companies.manage' },
  users: { view: 'users.view', manage: 'users.manage' },
  roles: { view: 'roles.view' },
  drivers: { view: 'drivers.view', create: 'drivers.create', update: 'drivers.update', delete: 'drivers.delete' },
  vehicles: { view: 'vehicles.view', create: 'vehicles.create', update: 'vehicles.update', delete: 'vehicles.delete' },
  implements: { view: 'implements.view', create: 'implements.create', update: 'implements.update', delete: 'implements.delete' },
  audit: { view: 'audit.view' },
  assignments: { view: 'assignments.view', manage: 'assignments.manage' },
  mileage: { record: 'mileage.record', manage: 'mileage.manage' },
  documents: { view: 'documents.view', manage: 'documents.manage', delete: 'documents.delete' },
  checklists: { view: 'checklists.view', execute: 'checklists.execute' },
  occurrences: { view: 'occurrences.view', create: 'occurrences.create', manage: 'occurrences.manage' },
  operations: { configure: 'operations.configure' },
} as const;

type Values<T> = T[keyof T];
export type Permission = Values<{ [K in keyof typeof PERMISSIONS]: Values<(typeof PERMISSIONS)[K]> }>;
