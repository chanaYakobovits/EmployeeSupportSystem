import { convertToParamMap } from '@angular/router';

import {
  ListStateDef,
  boolParam,
  enumParam,
  hasActiveFilters,
  idParam,
  readListState,
  textParam,
  toPageQuery,
  toQueryParams,
  withFilters,
  withPage,
  withSort,
  withoutFilters,
} from './list-state';

type Filters = {
  search: string | null;
  departmentId: number | null;
  role: 'Handler' | 'Admin' | null;
  isActive: boolean | null;
};

const DEF: ListStateDef<Filters> = {
  filters: { search: textParam, departmentId: idParam, role: enumParam(['Handler', 'Admin']), isActive: boolParam },
  sortFields: ['name', 'email', 'createdAt'],
  defaultSort: { by: 'name', dir: 'asc' },
};

function read(params: Record<string, string>) {
  return readListState(convertToParamMap(params), DEF);
}

/** The params as they end up in the URL (null = removed). */
function urlParams(params: Record<string, string | null>): Record<string, string> {
  return Object.fromEntries(Object.entries(params).filter(([, value]) => value !== null)) as Record<string, string>;
}

describe('list-state', () => {
  it('an empty URL is the default state', () => {
    const state = read({});
    expect(state).toEqual({
      filters: { search: null, departmentId: null, role: null, isActive: null },
      page: 1,
      pageSize: 25,
      sort: { by: 'name', dir: 'asc' },
    });
    expect(hasActiveFilters(state)).toBeFalse();
  });

  it('reads filters, paging and sort from the URL', () => {
    const state = read({
      search: 'דנה',
      departmentId: '3',
      role: 'Handler',
      isActive: 'false',
      page: '4',
      pageSize: '50',
      sortBy: 'email',
      sortDir: 'desc',
    });
    expect(state.filters).toEqual({ search: 'דנה', departmentId: 3, role: 'Handler', isActive: false });
    expect(state.page).toBe(4);
    expect(state.pageSize).toBe(50);
    expect(state.sort).toEqual({ by: 'email', dir: 'desc' });
    expect(hasActiveFilters(state)).toBeTrue();
  });

  it('round-trips: state → query params → state', () => {
    const params = { search: 'x', departmentId: '7', role: 'Admin', isActive: 'true', page: '2', pageSize: '10', sortBy: 'createdAt', sortDir: 'desc' };
    const state = read(params);
    expect(urlParams(toQueryParams(state, DEF))).toEqual(params);
    expect(read(urlParams(toQueryParams(state, DEF)))).toEqual(state);
  });

  it('does not write defaults to the URL', () => {
    expect(urlParams(toQueryParams(read({}), DEF))).toEqual({});
    expect(urlParams(toQueryParams(read({ page: '1', pageSize: '25', sortBy: 'name', sortDir: 'asc' }), DEF))).toEqual({});
  });

  it('the default column sorted the other way keeps only sortDir', () => {
    const state = withSort(read({}), { by: 'name', dir: 'desc' });
    expect(urlParams(toQueryParams(state, DEF))).toEqual({ sortDir: 'desc' });
    expect(read({ sortDir: 'desc' }).sort).toEqual({ by: 'name', dir: 'desc' });
  });

  it('ignores invalid values instead of failing', () => {
    const state = read({
      departmentId: 'abc',
      role: 'Hacker',
      isActive: 'maybe',
      page: '-2',
      pageSize: '1000',
      sortBy: 'passwordHash',
      sortDir: 'sideways',
      search: '   ',
    });
    expect(state).toEqual(read({}));
  });

  it('changing a filter or the sort goes back to page 1', () => {
    const state = read({ page: '5' });
    expect(withFilters(state, { search: 'abc' }).page).toBe(1);
    expect(withSort(state, { by: 'email', dir: 'asc' }).page).toBe(1);
    expect(withoutFilters(read({ page: '3', search: 'a', isActive: 'true' }))).toEqual(read({}));
  });

  it('changing the page size goes back to page 1; changing the page keeps the size', () => {
    const state = read({ page: '3' });
    expect(withPage(state, 4, 25)).toEqual(jasmine.objectContaining({ page: 4, pageSize: 25 }));
    expect(withPage(state, 3, 50)).toEqual(jasmine.objectContaining({ page: 1, pageSize: 50 }));
  });

  it('toPageQuery is the request: filters + paging + sort', () => {
    expect(toPageQuery(read({ search: 'a', page: '2' }))).toEqual({
      search: 'a',
      departmentId: null,
      role: null,
      isActive: null,
      page: 2,
      pageSize: 25,
      sortBy: 'name',
      sortDir: 'asc',
    });
  });
});
