import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { TicketStatus } from '../../../models/ticket.models';
import { formatDateTime } from '../../../pipes/date-time.pipe';
import { ClockService } from '../../../services/clock.service';
import { dueCountdown } from '../../../services/due-countdown';

/**
 * The SLA countdown (task 3.4): "נותרו 3 שעות", or "חריגה של שעתיים" in red with a warning icon (not colour alone –
 * spec 11.6). The due date (dd/MM/yyyy HH:mm, Israel time) in a tooltip and for screen readers. Moves once a minute.
 * Nothing while waiting for the employee or once resolved / closed / cancelled; "—" without a due time.
 */
@Component({
  selector: 'app-due-countdown',
  imports: [MatIconModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.overdue]': 'countdown()?.overdue === true' },
  template: `
    @if (countdown(); as countdown) {
      <span class="countdown" [matTooltip]="'מועד יעד: ' + full()">
        <mat-icon aria-hidden="true">{{ countdown.overdue ? 'warning' : 'schedule' }}</mat-icon>
        <span>{{ countdown.text }}</span>
        <span class="visually-hidden">, מועד יעד {{ full() }}</span>
      </span>
    } @else if (!dueAt()) {
      <span class="muted" aria-label="אין מועד יעד">—</span>
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      white-space: nowrap;
      color: var(--text-secondary);
    }
    :host(.overdue) {
      color: var(--error-text);
      font-weight: 700;
    }
    .countdown {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
    }
  `,
})
export class DueCountdown {
  readonly dueAt = input.required<string | null>();
  readonly status = input.required<TicketStatus>();
  /** The server's flag when the data was loaded. */
  readonly isOverdue = input(false);

  private readonly clock = inject(ClockService);
  protected readonly countdown = computed(() =>
    dueCountdown(this.dueAt(), this.status(), this.isOverdue(), this.clock.now()),
  );
  protected readonly full = computed(() => formatDateTime(this.dueAt()));
}
