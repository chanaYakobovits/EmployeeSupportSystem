import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { Urgency } from '../../../core/models/sla.models';
import { urgencyLabel } from '../../pipes/urgency-label.pipe';

/**
 * Urgency as a coloured dot + Hebrew text (design guide): Low green, Medium orange, High red,
 * Critical dark red and bold. For handlers and managers only – employees do not see urgency.
 */
@Component({
  selector: 'app-urgency-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': '"urgency-" + urgency().toLowerCase()' },
  template: `<span class="dot" aria-hidden="true"></span>{{ label() }}`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      white-space: nowrap;
      color: var(--text-primary);
    }
    .dot {
      width: 8px;
      height: 8px;
      flex-shrink: 0;
      border-radius: 50%;
      background: var(--dot-color);
    }
    :host(.urgency-low) {
      --dot-color: var(--urgency-low);
    }
    :host(.urgency-medium) {
      --dot-color: var(--urgency-medium);
    }
    :host(.urgency-high) {
      --dot-color: var(--urgency-high);
    }
    :host(.urgency-critical) {
      --dot-color: var(--urgency-critical);
      font-weight: 700;
      color: var(--urgency-critical);
    }
  `,
})
export class UrgencyBadge {
  readonly urgency = input.required<Urgency>();
  protected readonly label = computed(() => urgencyLabel(this.urgency()));
}
