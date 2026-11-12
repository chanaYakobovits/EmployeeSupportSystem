import { Injectable, inject, signal } from '@angular/core';
import { Observable, defer, finalize } from 'rxjs';

import { TicketsApiService } from '../../../core/api/tickets-api.service';
import { CreateTicketRequest, TicketCreated } from '../../../core/models/ticket.models';

/** State of "פנייה חדשה" (spec 7.4). Provided by NewTicketPage. */
@Injectable()
export class NewTicketStore {
  private readonly api = inject(TicketsApiService);

  private readonly savingSignal = signal(false);
  /** True while the ticket is being sent: the send button is disabled (no double submit, spec 7.4). */
  readonly saving = this.savingSignal.asReadonly();

  create(request: CreateTicketRequest): Observable<TicketCreated> {
    return defer(() => {
      this.savingSignal.set(true);
      return this.api.create(request).pipe(finalize(() => this.savingSignal.set(false)));
    });
  }
}
