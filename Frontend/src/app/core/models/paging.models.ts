/** Sort direction of a list endpoint (spec 9.4). */
export type SortDir = 'asc' | 'desc';

/** Paging and sorting parameters of a list endpoint (Backend Common.DTOs.Paging.PageQuery). */
export interface PageQuery {
  page: number;
  pageSize: number;
  /** Omitted → the list's default order. */
  sortBy?: string;
  sortDir?: SortDir;
}

/** One page of a list (Backend PagedResult<T>). totalCount counts all matching rows, not just this page. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Spec 9.4: default 25, maximum 100. */
export const DEFAULT_PAGE_SIZE = 25;
export const PAGE_SIZE_OPTIONS: readonly number[] = [10, 25, 50, 100];
