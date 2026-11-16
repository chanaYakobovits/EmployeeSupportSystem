import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { Observable, catchError, finalize, map, of, tap } from 'rxjs';

import { AuthService } from './auth.service';
import { CategoriesApiService } from './categories-api.service';
import { TicketsApiService } from './tickets-api.service';
import { ListStateDef, boolParam, dateParam, enumParam, idParam, listParam, textParam, toPageQuery } from './list-state';
import { ListStore } from './list-store';
import { Category } from '../models/category.models';
import {
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

  constructor() {
    this.categoriesApi
      .list()
      .pipe(
        catchError(() => of([] as Category[])),
        takeUntilDestroyed(),
      )
      .subscribe((categories) => this.categoriesSignal.set(categories));
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
