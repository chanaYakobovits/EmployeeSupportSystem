import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Injector,
  effect,
  inject,
  input,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';

import { HasUnsavedChanges, unsavedMessage } from '../../../guards/unsaved-changes.guard';
import { toUserMessage } from '../../../interceptors/error.interceptor';
import { DateTimePipe } from '../../../pipes/date-time.pipe';
import { idParam } from '../../../services/list-state';
import { NotificationService } from '../../../services/notification.service';
import { ChangeOutcome, TicketHandleStore, isConflictError } from '../../../services/ticket-handle.store';
import { ticketUrl as employeeTicketUrl } from '../../employee/employee-urls';
import { AttachmentChip } from '../../shared/attachment-chip/attachment-chip';
import { ClassificationSourceBadge } from '../../shared/classification-source-badge/classification-source-badge';
import { DueCountdown } from '../../shared/due-countdown/due-countdown';
import { EmptyState } from '../../shared/empty-state/empty-state';
import { ErrorState } from '../../shared/error-state/error-state';
import { LoadingState } from '../../shared/loading-state/loading-state';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { Timeline } from '../../shared/timeline/timeline';
import { UrgencyBadge } from '../../shared/urgency-badge/urgency-badge';
import { CONFLICT_MESSAGE, LEFT_SCOPE_MESSAGE, TAKEN_MESSAGE } from '../handler-messages';
import { safeQueueUrl } from '../handler-urls';
import { openAssignDialog } from './assign-dialog/assign-dialog';
import { openReclassifyDialog } from './reclassify-dialog/reclassify-dialog';
import { TicketComposer } from './ticket-composer/ticket-composer';

export const RECLASSIFIED_MESSAGE = 'הסיווג עודכן';
export const STARTED_MESSAGE = 'הטיפול בפנייה התחיל';
export const TRANSFERRED_MESSAGE = 'הפנייה הועברה';

/**
 * The handling screen (spec 7.7), by areas: 1 – the ticket and the employee; 2 – the AI analysis (task 4.4);
 * 3 – writing (internal note / reply); 4 – actions; 5 – the full timeline. Every action and writing mode is shown only
 * when the server allows it (allowedActions) – nothing is computed here. A ticket the user opened is shown on the
 * employee's screen instead. After a change that moved the ticket out of the user's scope → a message, back to the queue.
 */
@Component({
  selector: 'app-ticket-handle-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    LoadingState,
    ErrorState,
    EmptyState,
    StatusBadge,
    UrgencyBadge,
    DueCountdown,
    ClassificationSourceBadge,
    AttachmentChip,
    Timeline,
    TicketComposer,
    DateTimePipe,
  ],
  providers: [TicketHandleStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(window:beforeunload)': 'onBeforeUnload($event)' },
  templateUrl: './ticket-handle-page.html',
  styleUrl: './ticket-handle-page.scss',
})
export class TicketHandlePage implements HasUnsavedChanges {
  /** Route parameter :id. */
  readonly id = input.required<string>();

  protected readonly store = inject(TicketHandleStore);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly injector = inject(Injector);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly composer = viewChild(TicketComposer);

  protected readonly ticket = this.store.ticket;
  /** The queue view the user came from (tab + filters), or the default tab. */
  protected readonly queueUrl = safeQueueUrl(this.router.getCurrentNavigation()?.extras.state ?? history.state);
  /** Leaving because the ticket left the user's scope: nothing to keep. */
  private leaving = false;

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });

    // The user opened this ticket: they see it as its requester (spec 2.4).
    effect(() => {
      if (this.store.status() === 'requesterView') {
        const id = idParam.parse(untracked(this.id));
        if (id !== null) {
          void this.router.navigateByUrl(employeeTicketUrl(id), { replaceUrl: true });
        }
      }
    });
  }

  /** Text typed or files chosen in the writing area and not sent: leaving asks first (unsavedChangesGuard). */
  hasUnsavedChanges(): boolean {
    const composer = this.composer();
    return !this.leaving && !!composer && (composer.hasText() || composer.hasFiles());
  }

  unsavedChangesMessage(): string {
    const composer = this.composer();
    return unsavedMessage(!!composer?.hasText(), !!composer?.hasFiles());
  }

  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  protected reload(): void {
    this.load(this.id());
  }

  /** "קח לטיפולי". 409 → the store reloaded the ticket; the message says so. */
  protected take(): void {
    this.runAction(this.store.take(), TAKEN_MESSAGE);
  }

  /** "התחל טיפול" (Assigned → InProgress). 409 → reloaded + the message of 11.2; 422 → the server's title. */
  protected start(): void {
    this.runAction(this.store.start(), STARTED_MESSAGE);
  }

  private runAction(action: Observable<ChangeOutcome>, success: string): void {
    action.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (outcome) => this.onOutcome(outcome, success),
      error: (error: unknown) =>
        this.notifications.error(
          isConflictError(error)
            ? CONFLICT_MESSAGE
            : toUserMessage(error instanceof HttpErrorResponse ? error : new HttpErrorResponse({ status: 500 })),
        ),
    });
  }

  protected openReclassify(): void {
    openReclassifyDialog(this.dialog, this.injector)
      .afterClosed()
      .subscribe((outcome) => this.onOutcome(outcome, RECLASSIFIED_MESSAGE));
  }

  protected openAssign(): void {
    openAssignDialog(this.dialog, this.injector)
      .afterClosed()
      .subscribe((outcome) => this.onOutcome(outcome, TRANSFERRED_MESSAGE));
  }

  /**
   * Updated → a confirmation (none when the caller showed its own); out of the user's scope → a message and back to the
   * queue. Cancelled → nothing.
   */
  protected onOutcome(outcome: ChangeOutcome | undefined, success?: string): void {
    if (outcome === 'updated' && success) {
      this.notifications.success(success);
    } else if (outcome === 'left') {
      this.leaving = true;
      this.notifications.success(LEFT_SCOPE_MESSAGE);
      void this.router.navigateByUrl(this.queueUrl);
    }
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
