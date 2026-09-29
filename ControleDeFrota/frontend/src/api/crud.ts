import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type ListParams = Record<string, string | number | undefined | null>;

/** REST resource with the standard list/get/create/update/delete contract of the API. */
export function createResource<TListItem, TDetail, TRequest>(path: string) {
  const key = [path] as const;

  const client = {
    list: (params: ListParams) =>
      api.get<PagedResult<TListItem>>(path, { params: cleanParams(params) }).then((r) => r.data),
    get: (id: string) => api.get<TDetail>(`${path}/${id}`).then((r) => r.data),
    create: (body: TRequest) => api.post<TDetail>(path, body).then((r) => r.data),
    update: (id: string, body: TRequest) => api.put<TDetail>(`${path}/${id}`, body).then((r) => r.data),
    remove: (id: string) => api.delete(`${path}/${id}`).then(() => undefined),
  };

  function useList(params: ListParams) {
    return useQuery({
      queryKey: [...key, 'list', params],
      queryFn: () => client.list(params),
      // Keeps the current rows visible while the next page/filter loads (no flicker).
      placeholderData: keepPreviousData,
    });
  }

  function useDetail(id: string | undefined) {
    return useQuery({ queryKey: [...key, 'detail', id], queryFn: () => client.get(id!), enabled: !!id });
  }

  function useSave(id: string | undefined) {
    const queryClient = useQueryClient();
    return useMutation({
      mutationFn: (body: TRequest) => (id ? client.update(id, body) : client.create(body)),
      onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
    });
  }

  function useRemove() {
    const queryClient = useQueryClient();
    return useMutation({
      mutationFn: client.remove,
      onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
    });
  }

  return { key, client, useList, useDetail, useSave, useRemove };
}

const cleanParams = (params: ListParams) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));
