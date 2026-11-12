import { ChangeDetectionStrategy, Component, effect, inject, input, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../../shared/components/error-state/error-state';
import { LoadingState } from '../../../shared/components/loading-state/loading-state';
import { StatusBadge } from '../../../shared/components/status-badge/status-badge';
import { idParam } from '../../../shared/list/list-state';
import { DateTimePipe } from '../../../shared/pipes/date-time.pipe';
import { MY_TICKETS_URL } from '../employee-urls';
import { TicketTimeline } from './ticket-timeline/ticket-timeline';
import { TicketViewStore } from './ticket-view.store';

/**
 * The employee's view of their ticket (spec 7.5): details, the description as entered, and the status timeline.
 * Not shown: AI analysis, urgency, internal notes. Comments, files and actions come in tasks 3.2 and 3.5.
 */
@Component({
  selector: 'app-ticket-view-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    LoadingState,
    ErrorState,
    EmptyState,
    StatusBadge,
    TicketTimeline,
    DateTimePipe,
  ],
  providers: [TicketViewStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-view-page.html',
  styleUrl: './ticket-view-page.scss',
})
export class TicketViewPage {
  /** Route parameter :id. */
  readonly id = input.required<string>();

  protected readonly store = inject(TicketViewStore);
  protected readonly ticket = this.store.ticket;
  protected readonly myTicketsUrl = MY_TICKETS_URL;

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });
  }

  protected reload(): void {
    this.load(this.id());
  }

  private load(raw: string): void {
    const id = idParam.parse(raw);
    if (id === null) {
      this.store.markNotFound();
    } else {
      this.store.load(id);
    }
  }
}
