import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { skipErrorToast } from './http-context';
import { toHttpParams } from './query-params';
import { PagedResult } from '../models/paging.models';
import { TicketAttachment } from '../models/attachment.models';
import {
  CommentCreated,
  CreateTicketRequest,
  MyTicketsQuery,
  RequesterTicketDetails,
  RequesterTicketHistory,
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

  /**
   * Opens a ticket for the signed-in user. 429 after 20 tickets in an hour.
   * The server accepts multipart/form-data only (task 3.2; JSON → 415). The Content-Type header is not set here:
   * the browser sets it with the multipart boundary. Every file under the field name "files".
   */
  create(request: CreateTicketRequest): Observable<TicketCreated> {
    return this.http.post<TicketCreated>(this.baseUrl, toCreateTicketForm(request), { context: skipErrorToast() });
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

  /** Status changes, comments and files, oldest first – only what the server lets this user see. 404 outside the scope. */
  history(id: number): Observable<RequesterTicketHistory> {
    return this.http.get<RequesterTicketHistory>(`${this.baseUrl}/${id}/history`, { context: skipErrorToast() });
  }

  /**
   * A visible comment (body 1–4,000 after trimming) with up to 3 files. Never an internal note from here (isInternal is
   * not sent; the server's default is false). 422 ticket-closed when the ticket is closed or cancelled; 413 when too large.
   */
  addComment(id: number, body: string, files: readonly File[]): Observable<CommentCreated> {
    const form = new FormData();
    form.append('body', body);
    appendFiles(form, files);
    return this.http.post<CommentCreated>(`${this.baseUrl}/${id}/comments`, form, { context: skipErrorToast() });
  }

  /** 1–3 files without a comment, visible to the requester. Same errors as addComment. */
  addAttachments(id: number, files: readonly File[]): Observable<TicketAttachment[]> {
    const form = new FormData();
    appendFiles(form, files);
    return this.http.post<TicketAttachment[]>(`${this.baseUrl}/${id}/attachments`, form, { context: skipErrorToast() });
  }

  /** Download address of an attachment – fetched through HttpClient (AttachmentDownloadService), so the token is sent. */
  attachmentUrl(ticketId: number, attachmentId: number): string {
    return `${this.baseUrl}/${ticketId}/attachments/${attachmentId}`;
  }
}

function appendFiles(form: FormData, files: readonly File[]): void {
  for (const file of files) {
    form.append('files', file, file.name);
  }
}

/** The form fields of POST /api/tickets. A missing subject is simply not sent (the server stores null). */
function toCreateTicketForm(request: CreateTicketRequest): FormData {
  const form = new FormData();
  if (request.subject !== null) {
    form.append('subject', request.subject);
  }
  form.append('description', request.description);
  appendFiles(form, request.files);
  return form;
}
