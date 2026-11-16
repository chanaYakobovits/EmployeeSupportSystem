import { ChangeDetectionStrategy, Component, effect, inject, input, untracked, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';

import { EmptyState } from '../../shared/empty-state/empty-state';
import { ErrorState } from '../../shared/error-state/error-state';
import { LoadingState } from '../../shared/loading-state/loading-state';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { idParam } from '../../../services/list-state';
import { DateTimePipe } from '../../../pipes/date-time.pipe';
import { MY_TICKETS_URL } from '../employee-urls';
import { Timeline } from '../../shared/timeline/timeline';
import { HasUnsavedChanges, unsavedMessage } from '../../../guards/unsaved-changes.guard';
import { TicketViewStore } from '../../../services/ticket-view.store';
import { TicketReply } from './ticket-reply/ticket-reply';
import { AuthService } from '../../../services/auth.service';
import { handlerTicketUrl } from '../../handler/handler-urls';

/**
 * The employee's view of their ticket (spec 7.5): details, the description as entered, one timeline of status changes,
 * comments and files (GET /history), and a reply at the bottom – replaced by a note when the ticket is closed.
 * Not shown: AI analysis, urgency, internal notes – the server does not send them. Status actions come in task 3.5.
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
    Timeline,
    TicketReply,
    DateTimePipe,
  ],
  providers: [TicketViewStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(window:beforeunload)': 'onBeforeUnload($event)' },
  templateUrl: './ticket-view-page.html',
  styleUrl: './ticket-view-page.scss',
})
export class TicketViewPage implements HasUnsavedChanges {
  /** Route parameter :id. */
  readonly id = input.required<string>();

  protected readonly store = inject(TicketViewStore);
  protected readonly ticket = this.store.ticket;
  protected readonly myTicketsUrl = MY_TICKETS_URL;
  /** Not there while loading, or when the ticket is closed. */
  private readonly reply = viewChild(TicketReply);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });

    // Not the user's own ticket, but one they handle: it belongs on the handling screen (spec 7.7).
    effect(() => {
      if (this.store.status() === 'staffView' && this.auth.roles().includes('Handler')) {
        const id = idParam.parse(untracked(this.id));
        if (id !== null) {
          void this.router.navigateByUrl(handlerTicketUrl(id), { replaceUrl: true });
        }
      }
    });
  }

  /** Reply text typed or files chosen and not sent: leaving asks first (unsavedChangesGuard). */
  hasUnsavedChanges(): boolean {
    const reply = this.reply();
    return !!reply && (reply.hasText() || reply.hasFiles());
  }

  unsavedChangesMessage(): string {
    const reply = this.reply();
    return unsavedMessage(!!reply?.hasText(), !!reply?.hasFiles());
  }

  /** Refresh / closing the tab with a reply not sent: the browser's own "leave site?" prompt. */
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
      event.returnValue = '';
    }
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
