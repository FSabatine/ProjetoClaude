import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

export type SortDirection = 'Asc' | 'Desc';

export interface SortState {
  by: string;
  direction: SortDirection;
}

/**
 * List state (search, filters, sort, page) lives in the URL: links can be shared and
 * the browser back button restores the exact list the user was looking at.
 */
export function useListParams<F extends string>(filterKeys: readonly F[], defaultSort: SortState) {
  const [params, setParams] = useSearchParams();

  const search = params.get('search') ?? '';
  const page = Math.max(1, Number(params.get('page')) || 1);
  const sort: SortState = {
    by: params.get('sortBy') ?? defaultSort.by,
    direction: (params.get('sortDirection') as SortDirection) ?? defaultSort.direction,
  };
  const filterValues = filterKeys.map((k) => params.get(k) ?? '').join('|');
  const filters = useMemo(
    () => Object.fromEntries(filterKeys.map((k, i) => [k, filterValues.split('|')[i] || null])) as Record<F, string | null>,
    // eslint-disable-next-line react-hooks/exhaustive-deps -- filterValues is the serialized form of the filters
    [filterValues],
  );

  const update = useCallback(
    (changes: Record<string, string | number | null>, resetPage = true) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current);
          for (const [key, value] of Object.entries(changes)) {
            if (value === null || value === '') next.delete(key);
            else next.set(key, String(value));
          }
          if (resetPage) next.delete('page');
          return next;
        },
        { replace: true },
      ),
    [setParams],
  );

  const hasFilters = !!search || Object.values(filters).some(Boolean);

  return {
    search,
    page,
    sort,
    filters,
    hasFilters,
    setSearch: (value: string) => update({ search: value }),
    setFilter: (key: F, value: string | null) => update({ [key]: value }),
    setPage: (value: number) => update({ page: value }, false),
    setSort: (value: SortState) => update({ sortBy: value.by, sortDirection: value.direction }),
    clearFilters: () => update(Object.fromEntries([['search', null], ...filterKeys.map((k) => [k, null])])),
    /** Query-string params for the API. */
    apiParams: { search, page, pageSize: 20, sortBy: sort.by, sortDirection: sort.direction, ...filters },
  };
}
