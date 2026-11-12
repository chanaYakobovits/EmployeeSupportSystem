import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, Router, convertToParamMap } from '@angular/router';
import { BehaviorSubject } from 'rxjs';

import { toHttpParams } from '../../core/http/query-params';
import { PagedResult } from '../../core/models/paging.models';
import { ListStateDef, textParam, toPageQuery } from './list-state';
import { ListStore } from './list-store';

type Filters = { search: string | null };
interface Item {
  id: number;
}

const DEF: ListStateDef<Filters> = {
  filters: { search: textParam },
  sortFields: ['name'],
  defaultSort: { by: 'name', dir: 'asc' },
};

function page(ids: number[], totalCount = ids.length, pageNumber = 1): PagedResult<Item> {
  return { items: ids.map((id) => ({ id })), page: pageNumber, pageSize: 25, totalCount };
}

describe('ListStore', () => {
  let queryParams: BehaviorSubject<ParamMap>;
  let navigate: jasmine.Spy;
  let http: HttpTestingController;

  beforeEach(() => {
    queryParams = new BehaviorSubject(convertToParamMap({}));
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { queryParamMap: queryParams } },
        { provide: Router, useValue: { navigate } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createStore(): ListStore<Filters, Item> {
    const client = TestBed.inject(HttpClient);
    return TestBed.runInInjectionContext(
      () =>
        new ListStore<Filters, Item>(DEF, (state) =>
          client.get<PagedResult<Item>>('/api/items', { params: toHttpParams({ ...toPageQuery(state) }) }),
        ),
    );
  }

  function expectRequest(search: string | null) {
    return http.expectOne((request) => request.url === '/api/items' && request.params.get('search') === search);
  }

  it('loads the page that matches the URL', () => {
    queryParams.next(convertToParamMap({ search: 'דנה', page: '2' }));
    const store = createStore();
    expect(store.status()).toBe('loading');

    const request = expectRequest('דנה');
    expect(request.request.params.get('page')).toBe('2');
    request.flush(page([1, 2], 27, 2));

    expect(store.status()).toBe('ready');
    expect(store.items().map((item) => item.id)).toEqual([1, 2]);
    expect(store.totalCount()).toBe(27);
    expect(store.state().filters.search).toBe('דנה');
  });

  it('cancels the previous request when the search changes (switchMap)', () => {
    const store = createStore();
    const first = expectRequest(null);

    queryParams.next(convertToParamMap({ search: 'ד' }));
    const second = expectRequest('ד');
    queryParams.next(convertToParamMap({ search: 'דנ' }));
    const third = expectRequest('דנ');

    expect(first.cancelled).toBeTrue();
    expect(second.cancelled).toBeTrue();
    expect(third.cancelled).toBeFalse();

    third.flush(page([9]));
    expect(store.items()).toEqual([{ id: 9 }]);
  });

  it('a search writes the URL (page back to 1, defaults removed), replacing the history entry', () => {
    queryParams.next(convertToParamMap({ page: '3' }));
    const store = createStore();
    expectRequest(null).flush(page([1], 80, 3));

    store.setFilters({ search: 'abc' }, { replaceUrl: true });

    expect(navigate).toHaveBeenCalledOnceWith(
      [],
      jasmine.objectContaining({
        queryParams: { search: 'abc', page: null, pageSize: null, sortBy: null, sortDir: null },
        replaceUrl: true,
      }),
    );
  });

  it('does not navigate when nothing changes', () => {
    queryParams.next(convertToParamMap({ search: 'abc' }));
    const store = createStore();
    expectRequest('abc').flush(page([]));

    store.setFilters({ search: 'abc' });
    expect(navigate).not.toHaveBeenCalled();
  });

  it('error → "error" state; retry loads again', () => {
    const store = createStore();
    expectRequest(null).flush({ title: 'x', status: 500 }, { status: 500, statusText: 'Server Error' });
    expect(store.status()).toBe('error');

    store.reload();
    expect(store.status()).toBe('loading');
    expectRequest(null).flush(page([5]));
    expect(store.status()).toBe('ready');
  });

  it('empty result: isEmpty, and hasActiveFilters tells "no results" from "no data"', () => {
    queryParams.next(convertToParamMap({ search: 'zzz' }));
    const store = createStore();
    expectRequest('zzz').flush(page([]));
    expect(store.isEmpty()).toBeTrue();
    expect(store.hasActiveFilters()).toBeTrue();
  });

  it('a page past the end goes to the last page', () => {
    queryParams.next(convertToParamMap({ page: '9' }));
    createStore();
    expectRequest(null).flush(page([], 30, 9));

    expect(navigate).toHaveBeenCalledOnceWith(
      [],
      jasmine.objectContaining({ queryParams: jasmine.objectContaining({ page: '2' }), replaceUrl: true }),
    );
  });
});
