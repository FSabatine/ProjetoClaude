import type { ReactNode } from 'react';
import { Paper, Skeleton, Stack } from '@mantine/core';
import type { UseQueryResult } from '@tanstack/react-query';
import { toApiError } from '../api/errors';
import { NotFoundPage } from '../pages/StatusPages';
import { ErrorState } from './States';

/**
 * Loading / not found / error handling shared by every create-edit page.
 * The form itself mounts only once data is ready, so its initial values are never stale.
 */
export function EntityFormPage<T>({ id, detail, children }: { id?: string; detail: UseQueryResult<T>; children: (data: T | undefined) => ReactNode }) {
  if (id && detail.isLoading) return <FormSkeleton />;
  if (id && detail.error) {
    return toApiError(detail.error).status === 404 ? <NotFoundPage /> : <Paper><ErrorState error={detail.error} onRetry={() => void detail.refetch()} /></Paper>;
  }
  return <>{children(detail.data)}</>;
}

export function FormSkeleton() {
  return (
    <Stack gap="md" aria-busy="true" aria-label="Carregando formulário">
      <Skeleton height={40} width="40%" />
      {[1, 2, 3].map((i) => (
        <Paper key={i} p="lg">
          <Skeleton height={18} width={160} mb="md" />
          <Stack gap="sm">
            <Skeleton height={36} />
            <Skeleton height={36} />
          </Stack>
        </Paper>
      ))}
    </Stack>
  );
}
