import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Center, Loader, Stack, Text } from '@mantine/core';
import { useAuth } from './AuthContext';
import type { Permission } from './permissions';
import { ForbiddenPage } from '../pages/StatusPages';

export function FullScreenLoader() {
  return (
    <Center h="100dvh">
      <Stack align="center" gap="sm">
        <Loader />
        <Text c="dimmed" size="sm">
          Carregando…
        </Text>
      </Stack>
    </Center>
  );
}

export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();
  if (status === 'loading') return <FullScreenLoader />;
  if (status === 'anonymous') return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  return <Outlet />;
}

export function RequirePermission({ permission, children }: { permission: Permission | Permission[]; children: ReactNode }) {
  const { can } = useAuth();
  const allowed = Array.isArray(permission) ? permission.some(can) : can(permission);
  return allowed ? <>{children}</> : <ForbiddenPage />;
}

/** Renders children only when the user has the permission. UX only; the API enforces access. */
export function Can({ permission, children }: { permission: Permission; children: ReactNode }) {
  return useAuth().can(permission) ? <>{children}</> : null;
}
