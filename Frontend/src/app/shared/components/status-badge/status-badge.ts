import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { TicketStatus } from '../../../core/models/ticket.models';
import { statusLabel } from '../../pipes/status-label.pipe';

/**
 * Ticket status as a "pill" (design guide): a 10% tint of the status colour behind text in the full colour.
 * Cancelled is lighter – an outline without a tint – but its text still passes WCAG AA. Colours: --status-* in styles.scss.
 */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': '"status-badge status-" + status()' },
  template: `{{ label() }}`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      padding: 2px 10px;
      border: 1px solid transparent;
      border-radius: 20px;
      font-size: 12px;
      font-weight: 600;
      line-height: 20px;
      white-space: nowrap;
      color: var(--status-color);
      background: color-mix(in srgb, var(--status-color) 10%, transparent);
    }
    :host(.status-New) {
      --status-color: var(--status-new);
    }
    :host(.status-Assigned) {
      --status-color: var(--status-assigned);
    }
    :host(.status-InProgress) {
      --status-color: var(--status-in-progress);
    }
    :host(.status-WaitingForEmployee) {
      --status-color: var(--status-waiting);
    }
    :host(.status-Resolved) {
      --status-color: var(--status-resolved);
    }
    :host(.status-Closed) {
      --status-color: var(--status-closed);
    }
    :host(.status-Cancelled) {
      --status-color: var(--status-cancelled);
      background: transparent;
      border-color: var(--border);
    }
  `,
})
export class StatusBadge {
  readonly status = input.required<TicketStatus>();
  protected readonly label = computed(() => statusLabel(this.status()));
}
