import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';

import { TicketsApiService } from '../../../core/api/tickets-api.service';
import { RequesterTicketDetails } from '../../../core/models/ticket.models';

export type TicketViewStatus = 'loading' | 'ready' | 'error' | 'notFound';

/** State of the employee's ticket view (spec 7.5). Provided by TicketViewPage. */
@Injectable()
export class TicketViewStore {
  private readonly api = inject(TicketsApiService);
  private readonly destroyRef = inject(DestroyRef);
  private loading?: Subscription;

  private readonly ticketSignal = signal<RequesterTicketDetails | null>(null);
  private readonly statusSignal = signal<TicketViewStatus>('loading');

  readonly ticket = this.ticketSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();

  load(id: number): void {
    this.loading?.unsubscribe();
    this.statusSignal.set('loading');
    this.loading = this.api
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (ticket) => {
          this.ticketSignal.set(ticket);
          this.statusSignal.set('ready');
        },
        // 404: no such ticket, or not the user's own (the server does not say which).
        error: (error: unknown) => {
          this.ticketSignal.set(null);
          this.statusSignal.set(error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error');
        },
      });
  }

  /** The route parameter is not a valid id. */
  markNotFound(): void {
    this.loading?.unsubscribe();
    this.ticketSignal.set(null);
    this.statusSignal.set('notFound');
  }
}
