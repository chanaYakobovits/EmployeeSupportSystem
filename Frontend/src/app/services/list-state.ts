import { ParamMap, Params } from '@angular/router';

import { DEFAULT_PAGE_SIZE, PAGE_SIZE_OPTIONS, PageQuery, SortDir } from '../models/paging.models';

/**
 * The state of a server-side list (search, filters, sort, page) and its mapping to the URL query string
 * (spec 10.4: refresh or "back" keeps the state). Pure functions – the URL is the source of truth,
 * see ListStore for the reactive part.
 */

/** How one filter is read from and written to the URL. parse returns null for an invalid value. */
export interface ParamCodec<T> {
  parse(raw: string): T | null;
  format(value: T): string;
}

/** Free text (search). Blank → not filtered. */
export const textParam: ParamCodec<string> = {
  parse: (raw) => raw.trim() || null,
  format: (value) => value,
};

/** A positive integer, e.g. an id. */
export const idParam: ParamCodec<number> = {
  parse: (raw) => (/^[1-9]\d{0,9}$/.test(raw) ? Number(raw) : null),
  format: (value) => String(value),
};

export const boolParam: ParamCodec<boolean> = {
  parse: (raw) => (raw === 'true' ? true : raw === 'false' ? false : null),
  format: (value) => String(value),
};

/** A calendar day, yyyy-MM-dd (e.g. a "from" date). */
export const dateParam: ParamCodec<string> = {
  parse: (raw) => (/^\d{4}-\d{2}-\d{2}$/.test(raw) && !Number.isNaN(Date.parse(`${raw}T00:00:00Z`)) ? raw : null),
  format: (value) => value,
};

/**
 * Several values of one codec in one query param, comma separated (?status=New,Assigned). Invalid values are dropped;
 * none left (or an empty list) → not filtered. The order of the URL is kept.
 */
export function listParam<T>(codec: ParamCodec<T>): ParamCodec<T[]> {
  return {
    parse: (raw) => {
      const values = raw
        .split(',')
        .map((part) => codec.parse(part.trim()))
        .filter((value): value is NonNullable<typeof value> => value !== null);
      const unique = [...new Set(values)] as T[];
      return unique.length > 0 ? unique : null;
    },
    format: (values) => values.map((value) => codec.format(value)).join(','),
  };
}

/** One of a closed set of values, e.g. a Role. */
export function enumParam<T extends string>(values: readonly T[]): ParamCodec<T> {
  return {
    parse: (raw) => (values.includes(raw as T) ? (raw as T) : null),
    format: (value) => value,
  };
}

/** Every filter of a list: null = not filtered. */
export type ListFilters = Record<string, unknown>;

export interface ListSort {
  by: string;
  dir: SortDir;
}

/** Definition of one list: its filters (query param name = filter key), sort fields and defaults. */
export interface ListStateDef<F extends ListFilters> {
  filters: { [K in keyof F]-?: ParamCodec<NonNullable<F[K]>> };
  /** Closed list of the endpoint's sortBy values (spec 9.4). */
  sortFields: readonly string[];
  /** The server's default order – not written to the URL. */
  defaultSort: ListSort;
  /**
   * Columns other than the default one that the server sorts descending when sortDir is not sent
   * (e.g. the newest first). Default: ascending.
   */
  descendingByDefault?: readonly string[];
  defaultPageSize?: number;
}

export interface ListState<F extends ListFilters> {
  filters: F;
  /** 1-based. */
  page: number;
  pageSize: number;
  sort: ListSort;
}

const PAGE = 'page';
const PAGE_SIZE = 'pageSize';
const SORT_BY = 'sortBy';
const SORT_DIR = 'sortDir';

/** Reads the state from the URL. Missing or invalid values fall back to the defaults. */
export function readListState<F extends ListFilters>(params: ParamMap, def: ListStateDef<F>): ListState<F> {
  const filters = {} as Record<string, unknown>;
  for (const key of filterKeys(def)) {
    const raw = params.get(key);
    filters[key] = raw === null ? null : def.filters[key].parse(raw);
  }

  const page = idParam.parse(params.get(PAGE) ?? '') ?? 1;
  const rawPageSize = Number(params.get(PAGE_SIZE));
  const pageSize = PAGE_SIZE_OPTIONS.includes(rawPageSize) ? rawPageSize : defaultPageSize(def);

  const rawSortBy = params.get(SORT_BY);
  const by = rawSortBy !== null && def.sortFields.includes(rawSortBy) ? rawSortBy : def.defaultSort.by;
  const rawSortDir = params.get(SORT_DIR);
  const dir: SortDir = rawSortDir === 'asc' || rawSortDir === 'desc' ? rawSortDir : defaultDir(by, def);

  return { filters: filters as F, page, pageSize, sort: { by, dir } };
}

/**
 * The query params of a state. Defaults and empty filters are null, so they are removed from the URL
 * (router.navigate with these params replaces the whole query string).
 */
export function toQueryParams<F extends ListFilters>(state: ListState<F>, def: ListStateDef<F>): Params {
  const params: Params = {};
  for (const key of filterKeys(def)) {
    const value = state.filters[key];
    params[key] = isEmptyFilter(value) ? null : def.filters[key].format(value as NonNullable<F[typeof key]>);
  }
  params[PAGE] = state.page > 1 ? String(state.page) : null;
  params[PAGE_SIZE] = state.pageSize !== defaultPageSize(def) ? String(state.pageSize) : null;
  params[SORT_BY] = state.sort.by !== def.defaultSort.by ? state.sort.by : null;
  params[SORT_DIR] = state.sort.dir !== defaultDir(state.sort.by, def) ? state.sort.dir : null;
  return params;
}

/** The request parameters of a state: the filters (null = not sent) + paging + sort. */
export function toPageQuery<F extends ListFilters>(state: ListState<F>): F & PageQuery {
  return {
    ...state.filters,
    page: state.page,
    pageSize: state.pageSize,
    sortBy: state.sort.by,
    sortDir: state.sort.dir,
  };
}

/** A filter changed: back to the first page, so the user does not land on a page that no longer exists. */
export function withFilters<F extends ListFilters>(state: ListState<F>, patch: Partial<F>): ListState<F> {
  return { ...state, filters: { ...state.filters, ...patch }, page: 1 };
}

export function withSort<F extends ListFilters>(state: ListState<F>, sort: ListSort): ListState<F> {
  return { ...state, sort, page: 1 };
}

export function withPage<F extends ListFilters>(state: ListState<F>, page: number, pageSize: number): ListState<F> {
  // A different page size changes what "page N" means – start over.
  return { ...state, page: pageSize === state.pageSize ? page : 1, pageSize };
}

export function withoutFilters<F extends ListFilters>(state: ListState<F>): ListState<F> {
  const filters = Object.fromEntries(Object.keys(state.filters).map((key) => [key, null])) as F;
  return { ...state, filters, page: 1 };
}

/** True when at least one filter (including the search) is set – "no results" rather than "no data". */
export function hasActiveFilters<F extends ListFilters>(state: ListState<F>): boolean {
  return Object.values(state.filters).some((value) => !isEmptyFilter(value));
}

/** Not filtered: null, undefined, or an empty list. */
function isEmptyFilter(value: unknown): boolean {
  return value === null || value === undefined || (Array.isArray(value) && value.length === 0);
}

/** Same URL ⇔ same state. */
export function sameListState<F extends ListFilters>(a: ListState<F>, b: ListState<F>, def: ListStateDef<F>): boolean {
  return JSON.stringify(toQueryParams(a, def)) === JSON.stringify(toQueryParams(b, def));
}

function filterKeys<F extends ListFilters>(def: ListStateDef<F>): (keyof F & string)[] {
  return Object.keys(def.filters) as (keyof F & string)[];
}

function defaultPageSize<F extends ListFilters>(def: ListStateDef<F>): number {
  return def.defaultPageSize ?? DEFAULT_PAGE_SIZE;
}

/** The default column sorts in its default direction; any other column ascending, unless descendingByDefault. */
export function defaultDir<F extends ListFilters>(by: string, def: ListStateDef<F>): SortDir {
  if (by === def.defaultSort.by) {
    return def.defaultSort.dir;
  }
  return def.descendingByDefault?.includes(by) ? 'desc' : 'asc';
}
