import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { Observable, catchError, map, of, switchMap, tap } from 'rxjs';

import { AuthService } from './auth.service';
import { DashboardApiService } from './dashboard-api.service';
import { DepartmentsApiService } from './departments-api.service';
import { ListSort, idParam } from './list-state';
import {
  DashboardMetrics,
  DashboardView,
  DepartmentDashboard,
  OrganizationDashboard,
} from '../models/dashboard.models';
import { Department } from '../models/department.models';
import {
  DashboardRangeChoice,
  rangeFromParams,
  rangeQuery,
  rangeToParams,
} from '../components/dashboard/dashboard-range';
import { DEFAULT_COMPARISON_SORT, sortComparison } from '../components/dashboard/department-comparison/comparison-sort';
import { departmentDashboardUrl } from '../components/dashboard/dashboard-urls';
import { REFRESHED_ANNOUNCEMENT } from '../components/dashboard/dashboard-messages';

/** Which dashboard a page shows – route data "dashboard". */
export type DashboardKind = 'department' | 'organization';

export type DashboardStatus = 'loading' | 'error' | 'notFound' | 'ready';

/** The URL value of the "handling" tab (?view=handling); missing = the employees' tickets. */
export const HANDLING_VIEW_PARAM = 'handling';

interface DashboardRequest {
  kind: DashboardKind;
  departmentId: number | null;
  view: DashboardView;
  range: DashboardRangeChoice;
  /** Bumped by refresh(): the same request again. */
  nonce: number;
}

type DashboardData = DepartmentDashboard | OrganizationDashboard;

/**
 * The state of a dashboard screen (spec 7.9), provided by the page. The URL is the source of truth: the department
 * (:id), the tab (?view=handling) and the range (?days / ?from&to). Every change of them → one request (switchMap);
 * refresh() repeats it and keeps the numbers on screen meanwhile.
 * The departments list (GET /api/departments, open to every signed-in user) gives the executive's selector and decides
 * whether the "handling" tab is shown.
 */
@Injectable()
export class DashboardStore {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(DashboardApiService);
  private readonly departmentsApi = inject(DepartmentsApiService);
  private readonly auth = inject(AuthService);

  readonly kind: DashboardKind = this.route.snapshot.data['dashboard'] === 'organization' ? 'organization' : 'department';

  private readonly queryParams = toSignal(this.route.queryParamMap, { initialValue: this.route.snapshot.queryParamMap });
  private readonly paramMap = toSignal(this.route.paramMap, { initialValue: this.route.snapshot.paramMap });

  readonly range = computed(() => rangeFromParams(this.queryParams()));
  readonly view = computed<DashboardView>(() =>
    this.kind === 'department' && this.queryParams().get('view') === HANDLING_VIEW_PARAM ? 'Handling' : 'Requesters',
  );
  readonly departmentId = computed(() => idParam.parse(this.paramMap().get('id') ?? ''));

  readonly isExecutive = computed(() => this.auth.roles().includes('Executive'));

  private readonly statusSignal = signal<DashboardStatus>('loading');
  private readonly dataSignal = signal<DashboardData | null>(null);
  private readonly refreshingSignal = signal(false);
  private readonly announcementSignal = signal('');
  private readonly nonce = signal(0);

  readonly status = this.statusSignal.asReadonly();
  /** The numbers of the current request (kept during a refresh). */
  readonly data = this.dataSignal.asReadonly();
  readonly refreshing = this.refreshingSignal.asReadonly();
  /** For an aria-live region: set after a refresh. */
  readonly announcement = this.announcementSignal.asReadonly();

  readonly departmentDashboard = computed(() => (this.kind === 'department' ? (this.data() as DepartmentDashboard | null) : null));
  readonly organizationDashboard = computed(() =>
    this.kind === 'organization' ? (this.data() as OrganizationDashboard | null) : null,
  );
  readonly metrics = computed<DashboardMetrics | null>(() => this.data());

  /** Active departments, by name; [] until loaded or when the list failed. */
  readonly departments = toSignal(
    this.departmentsApi.list().pipe(catchError(() => of([] as Department[]))),
    { initialValue: [] as Department[] },
  );

  /**
   * The "פניות בטיפול המחלקה" tab: shown when the department has active handlers (tickets reach a department only
   * through them – routing 3.3 and transfer 3.4), or when the URL already asks for it (handlers deactivated since).
   */
  readonly showHandlingTab = computed(() => {
    if (this.view() === 'Handling') {
      return true;
    }
    const id = this.departmentId();
    return this.departments().some((d) => d.id === id && d.handlerCount > 0);
  });

  private readonly comparisonSortSignal = signal<ListSort>(DEFAULT_COMPARISON_SORT);
  readonly comparisonSort = this.comparisonSortSignal.asReadonly();
  readonly comparisonRows = computed(() =>
    sortComparison(this.organizationDashboard()?.departments ?? [], this.comparisonSortSignal()),
  );

  /** Nothing at all: nothing opened or resolved in the range, and nothing open now. */
  readonly isEmpty = computed(() => {
    const cards = this.metrics()?.cards;
    return !!cards && cards.openedInRangeCount === 0 && cards.resolvedInRangeCount === 0 && cards.openCount === 0;
  });

  /** Nothing opened in the range – the charts (which count those) have nothing to show. */
  readonly isRangeEmpty = computed(() => this.metrics()?.cards.openedInRangeCount === 0);

  private readonly request = computed<DashboardRequest>(() => ({
    kind: this.kind,
    departmentId: this.departmentId(),
    view: this.view(),
    range: this.range(),
    nonce: this.nonce(),
  }));

  private lastKey = '';

  constructor() {
    toObservable(this.request)
      .pipe(
        switchMap((request) => this.load(request)),
        takeUntilDestroyed(),
      )
      .subscribe();
  }

  /** The same request again ("רענון"): the numbers stay on screen until the new ones arrive. */
  refresh(): void {
    this.nonce.update((n) => n + 1);
  }

  setRange(range: DashboardRangeChoice): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: rangeToParams(range), queryParamsHandling: 'merge' });
  }

  /** The query params of a tab (for its link): the range is kept. */
  viewParams(view: DashboardView): Params {
    return { view: view === 'Handling' ? HANDLING_VIEW_PARAM : null };
  }

  /** The range part of the current URL – kept when moving to another dashboard. */
  rangeParams(): Params {
    const params = rangeToParams(this.range());
    return Object.fromEntries(Object.entries(params).filter(([, value]) => value !== null));
  }

  /** Another department's dashboard, with the same range (the tab starts over). */
  openDepartment(id: number): void {
    void this.router.navigate([departmentDashboardUrl(id)], { queryParams: this.rangeParams() });
  }

  setComparisonSort(sort: ListSort): void {
    this.comparisonSortSignal.set(sort);
  }

  private load(request: DashboardRequest): Observable<unknown> {
    const key = `${request.kind}|${request.departmentId}|${request.view}|${JSON.stringify(request.range)}`;
    const isRefresh = key === this.lastKey && this.dataSignal() !== null && this.statusSignal() === 'ready';
    this.lastKey = key;

    if (request.kind === 'department' && request.departmentId === null) {
      this.dataSignal.set(null);
      this.statusSignal.set('notFound');
      return of(null);
    }

    if (isRefresh) {
      this.refreshingSignal.set(true);
    } else {
      this.dataSignal.set(null);
      this.statusSignal.set('loading');
    }
    this.announcementSignal.set('');

    const range = rangeQuery(request.range);
    const call: Observable<DashboardData> =
      request.kind === 'organization'
        ? this.api.organization(range)
        : this.api.department(request.departmentId!, { ...range, view: request.view === 'Handling' ? 'Handling' : null });

    return call.pipe(
      tap((data) => {
        this.dataSignal.set(data);
        this.statusSignal.set('ready');
        this.refreshingSignal.set(false);
        if (isRefresh) {
          this.announcementSignal.set(REFRESHED_ANNOUNCEMENT);
        }
      }),
      map(() => null),
      catchError((error: unknown) => {
        this.refreshingSignal.set(false);
        this.dataSignal.set(null);
        this.statusSignal.set(error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error');
        return of(null);
      }),
    );
  }
}
