import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { TicketStatus } from '../../../models/ticket.models';
import { statusLabel } from '../../../pipes/status-label.pipe';
import { statusColorVar } from '../../../services/status-colors';

/**
 * Ticket status as a "pill" (design guide): a 10% tint of the status colour behind text in the full colour.
 * Cancelled is lighter – an outline without a tint – but its text still passes WCAG AA.
 * The colour comes from TICKET_STATUS_COLOR_VARS (services/status-colors.ts) – the same as the dashboard's status chart.
 */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': '"status-badge status-" + status()',
    '[style.--status-color]': 'color()',
  },
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
    :host(.status-Cancelled) {
      background: transparent;
      border-color: var(--border);
    }
  `,
})
export class StatusBadge {
  readonly status = input.required<TicketStatus>();
  protected readonly label = computed(() => statusLabel(this.status()));
  protected readonly color = computed(() => statusColorVar(this.status()));
}
