import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, catchError, defer, finalize, forkJoin, map, of, switchMap, tap, throwError } from 'rxjs';

import { TicketsApiService } from './tickets-api.service';
import { problemType } from '../models/problem-details';
import {
  CLOSED_TICKET_STATUSES,
  RequesterTicketDetails,
  RequesterTicketHistory,
  isStaffTicketDetails,
} from '../models/ticket.models';

/** staffView: the user is staff on this ticket, not its requester – it belongs on the handling screen. */
export type TicketViewStatus = 'loading' | 'ready' | 'error' | 'notFound' | 'staffView';

/** The ticket is not the user's own: the server sent the staff view. */
class StaffViewError extends Error {}

interface TicketViewData {
  ticket: RequesterTicketDetails;
  history: RequesterTicketHistory;
}

/** 422 from the server when the ticket was closed or cancelled meanwhile. */
export function isTicketClosedError(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 422 && problemType(error) === 'ticket-closed';
}

/**
 * State of the employee's ticket view (spec 7.5): the ticket, its timeline (GET /history) and the reply.
 * Provided by TicketViewPage.
 */
@Injectable()
export class TicketViewStore {
  private readonly api = inject(TicketsApiService);
  private readonly destroyRef = inject(DestroyRef);
  private loading?: Subscription;
  private ticketId: number | null = null;

  private readonly ticketSignal = signal<RequesterTicketDetails | null>(null);
  private readonly historySignal = signal<RequesterTicketHistory | null>(null);
  private readonly statusSignal = signal<TicketViewStatus>('loading');
  private readonly sendingSignal = signal(false);
  /** The server answered 422 ticket-closed (closed by someone else after the page was loaded). */
  private readonly closedByServer = signal(false);

  readonly ticket = this.ticketSignal.asReadonly();
  readonly history = this.historySignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  /** True while a reply is being sent: the send button is disabled. */
  readonly sending = this.sendingSignal.asReadonly();

  /** Comments and files can be added – not to a closed or cancelled ticket (the server would answer 422). */
  readonly canReply = computed(() => {
    const ticket = this.ticketSignal();
    return ticket !== null && !CLOSED_TICKET_STATUSES.includes(ticket.status) && !this.closedByServer();
  });

  load(id: number): void {
    this.loading?.unsubscribe();
    this.ticketId = id;
    this.closedByServer.set(false);
    this.statusSignal.set('loading');
    this.loading = this.fetch(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.apply(data);
          this.statusSignal.set('ready');
        },
        // 404: no such ticket, or outside the user's scope (the server does not say which).
        error: (error: unknown) => {
          this.ticketSignal.set(null);
          this.historySignal.set(null);
          this.statusSignal.set(
            error instanceof StaffViewError
              ? 'staffView'
              : error instanceof HttpErrorResponse && error.status === 404
                ? 'notFound'
                : 'error',
          );
        },
      });
  }

  /** The route parameter is not a valid id. */
  markNotFound(): void {
    this.loading?.unsubscribe();
    this.ticketId = null;
    this.ticketSignal.set(null);
    this.historySignal.set(null);
    this.statusSignal.set('notFound');
  }

  /**
   * Sends the reply: text (with or without files) → a comment; files only → attachments without a comment.
   * Completes after the ticket and its timeline were loaded again, so the reply is already shown when the form resets.
   * 422 ticket-closed → canReply becomes false; 409 → the data is reloaded. The error is re-thrown for the form.
   */
  sendReply(body: string, files: readonly File[]): Observable<void> {
    const id = this.ticketId;
    if (id === null) {
      return throwError(() => new Error('No ticket loaded.'));
    }
    return defer(() => {
      this.sendingSignal.set(true);
      const request: Observable<unknown> = body
        ? this.api.addComment(id, body, files)
        : this.api.addAttachments(id, files);
      return request.pipe(
        catchError((error: unknown) => {
          if (isTicketClosedError(error)) {
            this.closedByServer.set(true);
          }
          if (isTicketClosedError(error) || (error instanceof HttpErrorResponse && error.status === 409)) {
            this.refreshQuietly(id);
          }
          return throwError(() => error);
        }),
        // Sent: reload what the employee sees. A failed reload does not make the reply fail.
        switchMap(() => this.fetch(id).pipe(catchError(() => of(null)))),
        tap((data) => {
          if (data && this.ticketId === id) {
            this.apply(data);
          }
        }),
        map(() => undefined),
        finalize(() => this.sendingSignal.set(false)),
      );
    });
  }

  private refreshQuietly(id: number): void {
    this.fetch(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          if (this.ticketId === id) {
            this.apply(data);
          }
        },
        error: () => {
          // Keep what is shown; the next load will try again.
        },
      });
  }

  /** The requester's view only; the staff view → StaffViewError (the page moves to the handling screen). */
  private fetch(id: number): Observable<TicketViewData> {
    return forkJoin({ ticket: this.api.get(id), history: this.api.history(id) }).pipe(
      map(({ ticket, history }) => {
        if (isStaffTicketDetails(ticket)) {
          throw new StaffViewError();
        }
        return { ticket, history: history as RequesterTicketHistory };
      }),
    );
  }

  private apply(data: TicketViewData): void {
    this.ticketSignal.set(data.ticket);
    this.historySignal.set(data.history);
  }
}
