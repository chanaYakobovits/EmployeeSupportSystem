import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Injector,
  computed,
  effect,
  inject,
  input,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';

import { EmptyState } from '../../shared/empty-state/empty-state';
import { ErrorState } from '../../shared/error-state/error-state';
import { LoadingState } from '../../shared/loading-state/loading-state';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { idParam } from '../../../services/list-state';
import { DateTimePipe, formatDate } from '../../../pipes/date-time.pipe';
import { MY_TICKETS_URL } from '../employee-urls';
import { Timeline } from '../../shared/timeline/timeline';
import { HasUnsavedChanges, unsavedMessage } from '../../../guards/unsaved-changes.guard';
import { toUserMessage } from '../../../interceptors/error.interceptor';
import { TicketStatus } from '../../../models/ticket.models';
import { ConfirmService } from '../../../services/confirm.service';
import { ConfirmDialogData } from '../../shared/confirm-dialog/confirm-dialog';
import { NotificationService } from '../../../services/notification.service';
import { TicketViewStore } from '../../../services/ticket-view.store';
import { TicketReply } from './ticket-reply/ticket-reply';
import { AuthService } from '../../../services/auth.service';
import { staffTicketUrl } from '../../handler/handler-urls';
import { openReopenDialog } from './reopen-dialog/reopen-dialog';
import {
  CANCELLED_MESSAGE,
  CANCEL_CONFIRM,
  CONFIRMED_MESSAGE,
  CONFIRM_RESOLVED_CONFIRM,
  EMPLOYEE_CONFLICT_MESSAGE,
  REOPENED_MESSAGE,
} from './ticket-view-messages';

/**
 * The employee's view of their ticket (spec 7.5): details, the description as entered, one timeline of status changes,
 * comments and files (GET /history), and a reply at the bottom – replaced by a note when the ticket is closed.
 * The status actions (cancel; "הבעיה נפתרה" / reopen on a resolved ticket) are shown only when the server allows them
 * (allowedActions). Not shown: AI analysis, urgency, internal notes – the server does not send them.
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
  private readonly confirm = inject(ConfirmService);
  private readonly dialog = inject(MatDialog);
  private readonly injector = inject(Injector);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  /** The handler marked the ticket resolved and the employee may answer: confirm or reopen. */
  protected readonly showResolvedArea = computed(
    () => this.ticket()?.status === 'Resolved' && (this.store.can('confirmResolved') || this.store.can('reopen')),
  );
  /** dd/MM/yyyy (Israel time) of the last day to reopen. */
  protected readonly reopenUntilText = computed(() => formatDate(this.store.reopenUntil()));

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });

    // Not the user's own ticket, but one they handle or manage: it belongs on the handling screen (spec 7.7) – the
    // handler area's for a Handler, the dashboard area's for a manager / executive (task 5.2).
    effect(() => {
      if (this.store.status() === 'staffView') {
        const id = idParam.parse(untracked(this.id));
        const url = id === null ? null : staffTicketUrl(id, this.auth.roles());
        if (url !== null) {
          void this.router.navigateByUrl(url, { replaceUrl: true });
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

  /** "ביטול פנייה" – after a confirmation that it cannot be undone. */
  protected cancelTicket(): void {
    this.confirmThenChange(CANCEL_CONFIRM, 'Cancelled', CANCELLED_MESSAGE);
  }

  /** "הבעיה נפתרה" – after a confirmation: the ticket is closed for good. */
  protected confirmResolved(): void {
    this.confirmThenChange(CONFIRM_RESOLVED_CONFIRM, 'Closed', CONFIRMED_MESSAGE);
  }

  /** "הבעיה לא נפתרה – פתיחה מחדש": a dialog with an optional message. */
  protected reopen(): void {
    openReopenDialog(this.dialog, this.injector)
      .afterClosed()
      .subscribe((reopened) => {
        if (reopened) {
          this.notifications.success(REOPENED_MESSAGE);
        }
      });
  }

  private confirmThenChange(
    data: ConfirmDialogData,
    newStatus: TicketStatus,
    success: string,
  ): void {
    this.confirm
      .confirm(data)
      .pipe(
        filter(Boolean),
        switchMap(() => this.store.changeStatus(newStatus)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => this.notifications.success(success),
        // 409 → reloaded (the store); 422 → the server's title (e.g. the transition is no longer allowed).
        error: (error: unknown) => {
          const httpError = error instanceof HttpErrorResponse ? error : new HttpErrorResponse({ status: 500 });
          this.notifications.error(httpError.status === 409 ? EMPLOYEE_CONFLICT_MESSAGE : toUserMessage(httpError));
        },
      });
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
