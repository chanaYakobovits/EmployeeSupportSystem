import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { skipErrorToast } from './http-context';
import { toHttpParams } from './query-params';
import { PagedResult } from '../models/paging.models';
import { TicketAttachment } from '../models/attachment.models';
import {
  AssignTicketRequest,
  ChangeTicketStatusRequest,
  CommentCreated,
  CreateTicketRequest,
  MyTicketsQuery,
  ReclassifyTicketRequest,
  RequesterTicketHistory,
  RequesterTicketListItem,
  RequesterTicketsSummary,
  StaffTicketHistory,
  StaffTicketListItem,
  StaffTicketsQuery,
  TicketAssignOptions,
  TicketCreated,
  TicketDetails,
  TicketUpdated,
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

  /** The staff lists (scope = the tab). 403 for a scope that is not the user's. */
  listStaff(query: StaffTicketsQuery): Observable<PagedResult<StaffTicketListItem>> {
    return this.http.get<PagedResult<StaffTicketListItem>>(this.baseUrl, {
      params: toHttpParams({ ...query }),
      context: skipErrorToast(),
    });
  }

  /**
   * 404 for a ticket outside the user's scope. The requester – even a staff member who opened it – gets the requester's
   * view; other staff get the staff view (isStaffTicketDetails).
   */
  get(id: number): Observable<TicketDetails> {
    return this.http.get<TicketDetails>(`${this.baseUrl}/${id}`, { context: skipErrorToast() });
  }

  /** Status changes, comments and files, oldest first – only what the server lets this user see. 404 outside the scope. */
  history(id: number): Observable<RequesterTicketHistory | StaffTicketHistory> {
    return this.http.get<RequesterTicketHistory | StaffTicketHistory>(`${this.baseUrl}/${id}/history`, {
      context: skipErrorToast(),
    });
  }

  /**
   * A comment (body 1–4,000 after trimming) with up to 3 files. isInternal omitted → not sent (the server's default is
   * false: visible) – the employee's screen never sends it. The handler's screen always sends it explicitly.
   * 422 ticket-closed when the ticket is closed or cancelled; 413 when too large; 403 when the mode is not allowed.
   */
  addComment(id: number, body: string, files: readonly File[], isInternal?: boolean): Observable<CommentCreated> {
    const form = new FormData();
    form.append('body', body);
    if (isInternal !== undefined) {
      form.append('isInternal', String(isInternal));
    }
    appendFiles(form, files);
    return this.http.post<CommentCreated>(`${this.baseUrl}/${id}/comments`, form, { context: skipErrorToast() });
  }

  /** 1–3 files without a comment, visible to the requester. Same errors as addComment. */
  addAttachments(id: number, files: readonly File[]): Observable<TicketAttachment[]> {
    const form = new FormData();
    appendFiles(form, files);
    return this.http.post<TicketAttachment[]>(`${this.baseUrl}/${id}/attachments`, form, { context: skipErrorToast() });
  }

  /** Category and/or urgency + a reason. 409 when the ticket changed since rowVersion; 422 when resolved or unchanged. */
  reclassify(id: number, request: ReclassifyTicketRequest): Observable<TicketUpdated> {
    return this.http.patch<TicketUpdated>(`${this.baseUrl}/${id}/classification`, request, { context: skipErrorToast() });
  }

  /** To a handler of the handling department (the user = "קח לטיפולי") or to another department's queue. */
  assign(id: number, request: AssignTicketRequest): Observable<TicketUpdated> {
    return this.http.post<TicketUpdated>(`${this.baseUrl}/${id}/assign`, request, { context: skipErrorToast() });
  }

  /**
   * A status change (spec 3.3): the action is (current status, newStatus, the user's role). JSON only – files go through
   * addAttachments afterwards. 400 on message (required for WaitingForEmployee / Resolved); 403 when the user may not
   * change the status; 409 when the ticket changed since rowVersion; 422 for a transition that is not allowed.
   */
  changeStatus(id: number, request: ChangeTicketStatusRequest): Observable<TicketUpdated> {
    return this.http.patch<TicketUpdated>(`${this.baseUrl}/${id}/status`, request, { context: skipErrorToast() });
  }

  /** The targets of the transfer dialog. 403 without the assign permission. */
  assignOptions(id: number): Observable<TicketAssignOptions> {
    return this.http.get<TicketAssignOptions>(`${this.baseUrl}/${id}/assign-options`, { context: skipErrorToast() });
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
