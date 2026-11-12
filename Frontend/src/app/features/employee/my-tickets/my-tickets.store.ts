import { Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, map, of, startWith, switchMap } from 'rxjs';

import { TicketsApiService } from '../../../core/api/tickets-api.service';
import {
  MY_TICKET_SORT_FIELDS,
  REQUESTER_STATUS_GROUPS,
  REQUESTER_STATUS_GROUP_VALUES,
  RequesterStatusGroup,
  RequesterTicketListItem,
  RequesterTicketsSummary,
} from '../../../core/models/ticket.models';
import { ListStateDef, enumParam, textParam, toPageQuery } from '../../../shared/list/list-state';
import { ListStore, LoadStatus } from '../../../shared/list/list-store';

export type MyTicketsFilters = {
  search: string | null;
  /** A status group (summary card / segmented filter); null = all statuses. */
  view: RequesterStatusGroup | null;
};

/** GET /api/tickets/my ↔ the query string of /employee. */
export const MY_TICKETS_LIST: ListStateDef<MyTicketsFilters> = {
  filters: {
    search: textParam,
    view: enumParam(REQUESTER_STATUS_GROUP_VALUES),
  },
  sortFields: MY_TICKET_SORT_FIELDS,
  // Newest first. Same default as the server (MyTicketSortFields.Default) – not written to the URL.
  defaultSort: { by: 'createdAt', dir: 'desc' },
};

/** State of "הפניות שלי" (spec 7.3). Provided by MyTicketsPage. */
@Injectable()
export class MyTicketsStore {
  private readonly api = inject(TicketsApiService);
  private readonly reloadSummary$ = new Subject<void>();

  readonly list = new ListStore<MyTicketsFilters, RequesterTicketListItem>(MY_TICKETS_LIST, (state) => {
    const { view, ...query } = toPageQuery(state);
    return this.api.listMine({ ...query, status: view ? REQUESTER_STATUS_GROUPS[view] : null });
  });

  private readonly summarySignal = signal<RequesterTicketsSummary | null>(null);
  private readonly summaryStatusSignal = signal<LoadStatus>('loading');
  /** The summary cards' numbers; null while loading or after an error (the cards show "—"). */
  readonly summary = this.summarySignal.asReadonly();
  readonly summaryStatus = this.summaryStatusSignal.asReadonly();

  constructor() {
    this.reloadSummary$
      .pipe(
        startWith(undefined),
        switchMap(() => {
          this.summaryStatusSignal.set('loading');
          return this.api.mySummary().pipe(
            map((summary): RequesterTicketsSummary | null => summary),
            catchError(() => of(null)),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((summary) => {
        this.summarySignal.set(summary);
        this.summaryStatusSignal.set(summary === null ? 'error' : 'ready');
      });
  }

  /** "Try again": the list and the summary. */
  reload(): void {
    this.list.reload();
    this.reloadSummary$.next();
  }
}
