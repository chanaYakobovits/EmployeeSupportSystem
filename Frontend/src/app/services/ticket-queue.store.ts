import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { EMPTY, Observable, catchError, finalize, map, of, tap } from 'rxjs';

import { AuthService } from './auth.service';
import { CategoriesApiService } from './categories-api.service';
import { TicketsApiService } from './tickets-api.service';
import { ListStateDef, boolParam, dateParam, enumParam, idParam, listParam, textParam, toPageQuery } from './list-state';
import { ListStore } from './list-store';
import { Category } from '../models/category.models';
import {
  BULK_LIMITS,
  BulkAssignOptions,
  BulkTicketItem,
  BulkUpdateResult,
  FINAL_TICKET_STATUSES,
  STAFF_TICKET_DESCENDING_BY_DEFAULT,
  STAFF_TICKET_SORT_FIELDS,
  StaffTicketListItem,
  TICKET_STATUS_VALUES,
  TicketListScope,
  TicketStatus,
  URGENCY_FILTER_VALUES,
  UrgencyFilter,
} from '../models/ticket.models';

export type TicketQueueFilters = {
  search: string | null;
  status: TicketStatus[] | null;
  categoryId: number | null;
  /** Includes NotSet ("לא נקבעה"). */
  urgency: UrgencyFilter[] | null;
  /** Opening day, yyyy-MM-dd (Israel time), both included. */
  from: string | null;
  to: string | null;
  /** true = overdue only; null = all (false is never written to the URL). */
  overdueOnly: true | null;
};

const overdueOnlyParam = {
  parse: (raw: string) => (boolParam.parse(raw) === true ? (true as const) : null),
  format: () => 'true',
};

/** GET /api/tickets ↔ the query string of /handler/<tab>. The tab is the route, not a filter ("clear" keeps it). */
export const TICKET_QUEUE_LIST: ListStateDef<TicketQueueFilters> = {
  filters: {
    search: textParam,
    status: listParam(enumParam(TICKET_STATUS_VALUES)),
    categoryId: idParam,
    urgency: listParam(enumParam(URGENCY_FILTER_VALUES)),
    from: dateParam,
    to: dateParam,
    overdueOnly: overdueOnlyParam,
  },
  sortFields: STAFF_TICKET_SORT_FIELDS,
  // "עדיפות" – the server's default (StaffTicketSortFields.Default), not written to the URL. Without a status filter
  // the server leaves out closed and cancelled tickets (see withFinalStatuses).
  defaultSort: { by: 'priority', dir: 'asc' },
  descendingByDefault: STAFF_TICKET_DESCENDING_BY_DEFAULT,
};

/** The status filter shows closed or cancelled tickets ("כולל סגורות ומבוטלות" is on). */
export function includesFinalStatuses(status: readonly TicketStatus[] | null): boolean {
  return status?.some((value) => FINAL_TICKET_STATUSES.includes(value)) ?? false;
}

/**
 * The status filter after "כולל סגורות ומבוטלות" is switched. On: the selected statuses (none = all of them) and also
 * Closed and Cancelled – an explicit list, since the server's default leaves them out. Off: without them; nothing left
 * or every other status → null (the default, not in the URL).
 */
export function withFinalStatuses(status: readonly TicketStatus[] | null, include: boolean): TicketStatus[] | null {
  if (include) {
    const selected = status ?? TICKET_STATUS_VALUES;
    return TICKET_STATUS_VALUES.filter((value) => selected.includes(value) || FINAL_TICKET_STATUSES.includes(value));
  }
  const rest = (status ?? []).filter((value) => !FINAL_TICKET_STATUSES.includes(value));
  const allOthers = TICKET_STATUS_VALUES.filter((value) => !FINAL_TICKET_STATUSES.includes(value));
  return rest.length === 0 || rest.length === allOthers.length ? null : rest;
}

/** The target of a bulk transfer: a handler of the user's department, or another department's queue. */
export type BulkAssignTarget = { targetUserId: number } | { targetDepartmentId: number };

/** The result of "קח לטיפולי" from the list. */
export type TakeResult = 'taken' | 'conflict' | 'error';

/**
 * State of the handler's queue (spec 7.6): one tab = one scope of GET /api/tickets (route data "scope").
 * Provided by TicketQueuePage, which is created again for every tab.
 */
@Injectable()
export class TicketQueueStore {
  private readonly api = inject(TicketsApiService);
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly auth = inject(AuthService);

  readonly scope: TicketListScope = inject(ActivatedRoute).snapshot.data['scope'] ?? 'assigned';

  readonly list = new ListStore<TicketQueueFilters, StaffTicketListItem>(TICKET_QUEUE_LIST, (state) =>
    this.api.listStaff({ ...toPageQuery(state), scope: this.scope }),
  );

  private readonly categoriesSignal = signal<Category[]>([]);
  /** Active categories for the filter; empty when they could not be loaded (the filter is then hidden). */
  readonly categories = this.categoriesSignal.asReadonly();

  private readonly takingSignal = signal<number | null>(null);
  /** The ticket being taken ("קח לטיפולי"): its button is busy, the others are disabled. */
  readonly taking = this.takingSignal.asReadonly();
  readonly isTaking = computed(() => this.takingSignal() !== null);

  // ===================== Bulk selection (task 3.6) =====================

  private readonly selectedSignal = signal<ReadonlySet<number>>(new Set());
  /** Selected tickets – of the current page only (no selection across pages). */
  readonly selectedIds = this.selectedSignal.asReadonly();
  readonly selectedCount = computed(() => this.selectedSignal().size);
  readonly allOnPageSelected = computed(() => {
    const items = this.list.items();
    return items.length > 0 && items.every((ticket) => this.selectedSignal().has(ticket.id));
  });
  readonly someOnPageSelected = computed(() => this.selectedCount() > 0 && !this.allOnPageSelected());
  /** Over BULK_LIMITS.maxItems (cannot happen with pages of up to 100, checked anyway): the actions are disabled. */
  readonly overBulkLimit = computed(() => this.selectedCount() > BULK_LIMITS.maxItems);

  private readonly bulkBusySignal = signal(false);
  /** A bulk action is running: the selection and the actions are locked. */
  readonly bulkBusy = this.bulkBusySignal.asReadonly();

  constructor() {
    // Another page, page size, filter or sort → the selection is cleared. A reload keeps the state object, so it does
    // not clear (the failures of a bulk action stay selected). Another tab is another page and store.
    effect(() => {
      this.list.state();
      untracked(() => this.selectedSignal.set(new Set()));
    });

    // After every load: only tickets still on the page stay selected (e.g. a ticket that left the queue).
    effect(() => {
      if (this.list.status() !== 'ready') {
        return;
      }
      const ids = new Set(this.list.items().map((ticket) => ticket.id));
      untracked(() => {
        const selected = this.selectedSignal();
        if ([...selected].some((id) => !ids.has(id))) {
          this.selectedSignal.set(new Set([...selected].filter((id) => ids.has(id))));
        }
      });
    });

    this.categoriesApi
      .list()
      .pipe(
        catchError(() => of([] as Category[])),
        takeUntilDestroyed(),
      )
      .subscribe((categories) => this.categoriesSignal.set(categories));
  }

  isSelected(id: number): boolean {
    return this.selectedSignal().has(id);
  }

  toggle(id: number): void {
    if (this.bulkBusy()) {
      return;
    }
    const next = new Set(this.selectedSignal());
    if (!next.delete(id)) {
      next.add(id);
    }
    this.selectedSignal.set(next);
  }

  /** "בחירת כל הפניות בעמוד": the rows of this page only; when all of them are selected – none. */
  toggleAllOnPage(): void {
    if (this.bulkBusy()) {
      return;
    }
    this.selectedSignal.set(this.allOnPageSelected() ? new Set() : new Set(this.list.items().map((ticket) => ticket.id)));
  }

  clearSelection(): void {
    this.selectedSignal.set(new Set());
  }

  /** "התחל טיפול" on the selected tickets (Partial Success). */
  bulkStart(): Observable<BulkUpdateResult> {
    return this.runBulk((items) => this.api.bulkChangeStatus({ newStatus: 'InProgress', items }));
  }

  /** Transfers the selected tickets with one reason. A 400 (e.g. on reason) is the dialog's to show. */
  bulkAssign(target: BulkAssignTarget, reason: string): Observable<BulkUpdateResult> {
    return this.runBulk((items) => this.api.bulkAssign({ ...target, reason, items }));
  }

  /** The targets of the bulk transfer dialog. */
  bulkAssignOptions(): Observable<BulkAssignOptions> {
    return this.api.bulkAssignOptions();
  }

  /**
   * Sends the selected rows with the versions of the rows on the page. Whatever the answer, the list is reloaded; after
   * a result only the tickets that failed stay selected (those still on the page after the reload).
   */
  private runBulk(send: (items: BulkTicketItem[]) => Observable<BulkUpdateResult>): Observable<BulkUpdateResult> {
    const items = this.list
      .items()
      .filter((ticket) => this.selectedSignal().has(ticket.id))
      .map((ticket) => ({ id: ticket.id, rowVersion: ticket.rowVersion }));
    if (items.length === 0 || items.length > BULK_LIMITS.maxItems || this.bulkBusy()) {
      return EMPTY;
    }
    this.bulkBusySignal.set(true);
    return send(items).pipe(
      tap((result) => this.selectedSignal.set(new Set(result.failed.map((failure) => failure.id)))),
      finalize(() => {
        this.bulkBusySignal.set(false);
        this.list.reload();
      }),
    );
  }

  /**
   * "קח לטיפולי" from the queue tab: assigns the ticket to the signed-in handler with the version of the row.
   * Whatever the answer (taken, 409 – someone took or changed it first, 422 – no longer open), the list is reloaded.
   */
  take(ticket: StaffTicketListItem): Observable<TakeResult> {
    const userId = this.auth.currentUser()?.id;
    if (userId === undefined || this.isTaking()) {
      return of('error');
    }
    this.takingSignal.set(ticket.id);
    return this.api.assign(ticket.id, { targetUserId: userId, rowVersion: ticket.rowVersion }).pipe(
      map((): TakeResult => 'taken'),
      catchError((error: unknown) =>
        of<TakeResult>(error instanceof HttpErrorResponse && error.status === 409 ? 'conflict' : 'error'),
      ),
      tap(() => this.list.reload()),
      finalize(() => this.takingSignal.set(null)),
    );
  }
}
