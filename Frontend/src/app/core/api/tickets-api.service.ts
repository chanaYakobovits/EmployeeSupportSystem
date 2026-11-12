import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { skipErrorToast } from '../http/http-context';
import { toHttpParams } from '../http/query-params';
import { PagedResult } from '../models/paging.models';
import {
  CreateTicketRequest,
  MyTicketsQuery,
  RequesterTicketDetails,
  RequesterTicketListItem,
  RequesterTicketsSummary,
  TicketCreated,
} from '../models/ticket.models';

/**
 * Typed calls to the backend TicketsController (spec 9.2). No state.
 * Every screen that calls these shows the error itself (error state, field errors or an alert), so no toast.
 */
@Injectable({ providedIn: 'root' })
export class TicketsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/tickets`;

  /** Opens a ticket for the signed-in user. 429 after 20 tickets in an hour. */
  create(request: CreateTicketRequest): Observable<TicketCreated> {
    return this.http.post<TicketCreated>(this.baseUrl, request, { context: skipErrorToast() });
  }

  /** The signed-in user's own tickets. */
  listMine(query: MyTicketsQuery): Observable<PagedResult<RequesterTicketListItem>> {
    return this.http.get<PagedResult<RequesterTicketListItem>>(`${this.baseUrl}/my`, {
      params: toHttpParams({ ...query }),
      context: skipErrorToast(),
    });
  }

  mySummary(): Observable<RequesterTicketsSummary> {
    return this.http.get<RequesterTicketsSummary>(`${this.baseUrl}/my/summary`, { context: skipErrorToast() });
  }

  /** 404 for a ticket outside the user's scope. */
  get(id: number): Observable<RequesterTicketDetails> {
    return this.http.get<RequesterTicketDetails>(`${this.baseUrl}/${id}`, { context: skipErrorToast() });
  }
}
