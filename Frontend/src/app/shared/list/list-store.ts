import { Signal, WritableSignal, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router, convertToParamMap } from '@angular/router';
import { Observable, Subject, catchError, map, merge, of, switchMap, tap } from 'rxjs';

import { PagedResult } from '../../core/models/paging.models';
import {
  ListFilters,
  ListSort,
  ListState,
  ListStateDef,
  hasActiveFilters,
  readListState,
  sameListState,
  toQueryParams,
  withFilters,
  withPage,
  withSort,
  withoutFilters,
} from './list-state';

/** The view states of spec 7.10 for data that is loaded from the server ("empty" is ready with no items). */
export type LoadStatus = 'loading' | 'error' | 'ready';

export interface ListNavigationOptions {
  /** Replace the history entry instead of adding one (e.g. while typing a search). */
  replaceUrl?: boolean;
}

/**
 * State of a server-side list screen (spec 9.4, 10.4), shared by every list.
 * The URL query string is the source of truth: the actions navigate, and every change of the query params
 * loads the matching page. switchMap cancels the previous request, so only the latest answer is shown.
 *
 * Create it in a field of a screen's store (an injection context; the store is provided by the screen component,
 * so the subscription ends with the screen):
 *   readonly list = new ListStore(USERS_LIST, (query) => this.api.list(toPageQuery(query)));
 */
export class ListStore<F extends ListFilters, T> {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly reload$ = new Subject<void>();

  private readonly stateSignal: WritableSignal<ListState<F>>;
  private readonly statusSignal = signal<LoadStatus>('loading');
  private readonly resultSignal = signal<PagedResult<T> | null>(null);

  readonly state: Signal<ListState<F>>;
  readonly status = this.statusSignal.asReadonly();
  readonly items = computed(() => this.resultSignal()?.items ?? []);
  readonly totalCount = computed(() => this.resultSignal()?.totalCount ?? 0);
  readonly hasActiveFilters: Signal<boolean>;
  /** Loaded, and nothing to show. Use hasActiveFilters to tell "no data" from "no results". */
  readonly isEmpty = computed(() => this.status() === 'ready' && this.items().length === 0);
  /** The current query params, e.g. to come back to the same view from a details screen. */
  readonly queryParams: Signal<Params>;

  constructor(
    private readonly def: ListStateDef<F>,
    fetch: (state: ListState<F>) => Observable<PagedResult<T>>,
  ) {
    this.stateSignal = signal(readListState(convertToParamMap({}), def));
    this.state = this.stateSignal.asReadonly();
    this.hasActiveFilters = computed(() => hasActiveFilters(this.state()));
    this.queryParams = computed(() => toQueryParams(this.state(), def));

    merge(
      this.route.queryParamMap.pipe(map((params) => readListState(params, def))),
      this.reload$.pipe(map(() => this.stateSignal())),
    )
      .pipe(
        tap((state) => {
          this.stateSignal.set(state);
          this.statusSignal.set('loading');
        }),
        switchMap((state) =>
          fetch(state).pipe(
            map((result): PagedResult<T> | null => result),
            catchError(() => of(null)),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => this.onResult(result));
  }

  setFilters(patch: Partial<F>, options?: ListNavigationOptions): void {
    this.navigate(withFilters(this.state(), patch), options);
  }

  clearFilters(): void {
    this.navigate(withoutFilters(this.state()));
  }

  setSort(sort: ListSort): void {
    this.navigate(withSort(this.state(), sort));
  }

  /** page is 1-based. */
  setPage(page: number, pageSize: number): void {
    this.navigate(withPage(this.state(), page, pageSize));
  }

  /** "Try again" after an error, or a refresh after a change made elsewhere. */
  reload(): void {
    this.reload$.next();
  }

  private onResult(result: PagedResult<T> | null): void {
    if (result === null) {
      this.statusSignal.set('error');
      return;
    }

    const state = this.state();
    // The page no longer exists (rows were removed, or an old link): go to the last page that does.
    if (result.items.length === 0 && result.totalCount > 0 && state.page > 1) {
      const lastPage = Math.max(1, Math.ceil(result.totalCount / state.pageSize));
      this.navigate({ ...state, page: lastPage }, { replaceUrl: true });
      return;
    }

    this.resultSignal.set(result);
    this.statusSignal.set('ready');
  }

  private navigate(next: ListState<F>, options: ListNavigationOptions = {}): void {
    if (sameListState(next, this.state(), this.def)) {
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(next, this.def),
      replaceUrl: options.replaceUrl ?? false,
    });
  }
}
