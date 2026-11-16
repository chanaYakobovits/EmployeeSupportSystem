import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, catchError, defer, finalize, forkJoin, map, of, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';
import { CategoriesApiService } from './categories-api.service';
import { Category } from '../models/category.models';
import { TicketsApiService } from './tickets-api.service';
import { isTicketClosedError } from './ticket-view.store';
import {
  AssignTicketRequest,
  ReclassifyTicketRequest,
  StaffTicketDetails,
  StaffTicketHistory,
  TicketAction,
  TicketAssignOptions,
  TicketUpdated,
  isStaffTicketDetails,
} from '../models/ticket.models';

const OPENING_FILES_WINDOW_MS = 5_000;

/** requesterView: the user opened this ticket – it belongs on the employee's screen. */
export type TicketHandleStatus = 'loading' | 'ready' | 'error' | 'notFound' | 'requesterView';

/** After a change: still in the user's scope (reloaded), or moved out of it (back to the queue). */
export type ChangeOutcome = 'updated' | 'left';

/** 409 Conflict: someone else changed the ticket since it was loaded (spec 11.2). */
export function isConflictError(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 409;
}

interface TicketHandleData {
  ticket: StaffTicketDetails;
  history: StaffTicketHistory;
}

/** The ticket is the user's own: the server sent the requester's view. */
class RequesterViewError extends Error {}

/**
 * State of the handling screen (spec 7.7): the staff view of the ticket, its full timeline, and the changes.
 * Every change sends the version that is loaded now (spec 11.2). 409 → the ticket is reloaded quietly and the error is
 * re-thrown, so the form or dialog keeps what the user wrote and shows the message; the next try sends the new version.
 * Provided by TicketHandlePage; its dialogs get it through the page's injector.
 */
@Injectable()
export class TicketHandleStore {
  private readonly api = inject(TicketsApiService);
  private readonly auth = inject(AuthService);
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly destroyRef = inject(DestroyRef);
  private loading?: Subscription;
  private ticketId: number | null = null;

  private readonly ticketSignal = signal<StaffTicketDetails | null>(null);
  private readonly historySignal = signal<StaffTicketHistory | null>(null);
  private readonly statusSignal = signal<TicketHandleStatus>('loading');
  private readonly busySignal = signal(false);

  readonly ticket = this.ticketSignal.asReadonly();
  readonly history = this.historySignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  /** A change or a comment is being sent: the other actions wait. */
  readonly busy = this.busySignal.asReadonly();

  /** What the server allows now – the screen shows only these (never computed here). */
  readonly allowedActions = computed<readonly TicketAction[]>(() => this.ticketSignal()?.allowedActions ?? []);

  /**
   * The files of the ticket itself: attached without a comment together with the new-ticket form (saved in the same
   * transaction, so within a few seconds of the opening). Later files stay in the timeline only.
   */
  readonly openingAttachments = computed(() => {
    const ticket = this.ticketSignal();
    const history = this.historySignal();
    if (!ticket || !history) {
      return [];
    }
    const openedAt = Date.parse(ticket.createdAt);
    return history.items.flatMap((item) =>
      item.kind === 'attachment' && Math.abs(Date.parse(item.at) - openedAt) < OPENING_FILES_WINDOW_MS
        ? [item.attachment]
        : [],
    );
  });

  can(action: TicketAction): boolean {
    return this.allowedActions().includes(action);
  }

  load(id: number): void {
    this.loading?.unsubscribe();
    this.ticketId = id;
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
            error instanceof RequesterViewError
              ? 'requesterView'
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

  /** The active categories, for the reclassification dialog. */
  activeCategories(): Observable<Category[]> {
    return this.categoriesApi.list();
  }

  /** The targets of the transfer dialog (handlers of the handling department, other departments). */
  assignOptions(): Observable<TicketAssignOptions> {
    const id = this.ticketId;
    return id === null ? throwError(() => new Error('No ticket loaded.')) : this.api.assignOptions(id);
  }

  /** Category and/or urgency with a reason. */
  reclassify(change: Omit<ReclassifyTicketRequest, 'rowVersion'>): Observable<ChangeOutcome> {
    return this.change((id, rowVersion) => this.api.reclassify(id, { ...change, rowVersion }));
  }

  /** To a handler of the handling department, or to another department's queue, with a reason. */
  assign(target: Omit<AssignTicketRequest, 'rowVersion'>): Observable<ChangeOutcome> {
    return this.change((id, rowVersion) => this.api.assign(id, { ...target, rowVersion }));
  }

  /** "קח לטיפולי": to the signed-in handler, no reason (the server explains it itself). */
  take(): Observable<ChangeOutcome> {
    const userId = this.auth.currentUser()?.id;
    if (userId === undefined) {
      return throwError(() => new Error('No signed-in user.'));
    }
    return this.assign({ targetUserId: userId });
  }

  /**
   * A comment or an internal note (internal is always sent explicitly, so the server never guesses). Files without text
   * are allowed only for a visible reply (an internal file must belong to an internal note – the form checks it).
   * Completes after the ticket and its timeline were loaded again. 409 / 422 ticket-closed → reloaded, error re-thrown.
   */
  sendComment(body: string, files: readonly File[], internal: boolean): Observable<void> {
    const id = this.ticketId;
    if (id === null) {
      return throwError(() => new Error('No ticket loaded.'));
    }
    return defer(() => {
      this.busySignal.set(true);
      const request: Observable<unknown> =
        body || internal ? this.api.addComment(id, body, files, internal) : this.api.addAttachments(id, files);
      return request.pipe(
        catchError((error: unknown) => {
          if (isConflictError(error) || isTicketClosedError(error)) {
            this.refreshQuietly(id);
          }
          return throwError(() => error);
        }),
        switchMap(() => this.reloadAfterChange(id)),
        map(() => undefined),
        finalize(() => this.busySignal.set(false)),
      );
    });
  }

  /** Sends a change with the loaded version; reloads when the ticket is still in scope. */
  private change(send: (id: number, rowVersion: string) => Observable<TicketUpdated>): Observable<ChangeOutcome> {
    const ticket = this.ticketSignal();
    if (ticket === null) {
      return throwError(() => new Error('No ticket loaded.'));
    }
    const id = ticket.id;
    return defer(() => {
      this.busySignal.set(true);
      return send(id, ticket.rowVersion).pipe(
        catchError((error: unknown) => {
          // Someone else changed it: show the current data; the user's text stays in the dialog.
          if (isConflictError(error)) {
            this.refreshQuietly(id);
          }
          return throwError(() => error);
        }),
        switchMap((updated): Observable<ChangeOutcome> =>
          updated.canView ? this.reloadAfterChange(id).pipe(map(() => 'updated' as const)) : of('left' as const),
        ),
        finalize(() => this.busySignal.set(false)),
      );
    });
  }

  /** After a successful change: reload what is shown. A failed reload does not make the change fail. */
  private reloadAfterChange(id: number): Observable<void> {
    return this.fetch(id).pipe(
      map((data) => {
        if (this.ticketId === id) {
          this.apply(data);
        }
      }),
      catchError(() => of(undefined)),
    );
  }

  private refreshQuietly(id: number): void {
    this.reloadAfterChange(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  /** The staff view only; the requester's view → RequesterViewError (the page moves to the employee's screen). */
  private fetch(id: number): Observable<TicketHandleData> {
    return forkJoin({ ticket: this.api.get(id), history: this.api.history(id) }).pipe(
      map(({ ticket, history }) => {
        if (!isStaffTicketDetails(ticket)) {
          throw new RequesterViewError();
        }
        return { ticket, history: history as StaffTicketHistory };
      }),
    );
  }

  private apply(data: TicketHandleData): void {
    this.ticketSignal.set(data.ticket);
    this.historySignal.set(data.history);
  }
}
