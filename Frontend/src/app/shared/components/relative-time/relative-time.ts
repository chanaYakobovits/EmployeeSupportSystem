import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';

import { formatDateTime, parseApiDate } from '../../pipes/date-time.pipe';
import { formatRelativeTime } from '../../pipes/relative-time.pipe';

/**
 * "לפני שעתיים", with the full date (dd/MM/yyyy HH:mm, Israel time) in a tooltip and for screen readers.
 * <app-relative-time [value]="ticket.updatedAt" />
 */
@Component({
  selector: 'app-relative-time',
  imports: [MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <time [attr.datetime]="iso()" [matTooltip]="full()">
      <span aria-hidden="true">{{ relative() }}</span>
      <span class="visually-hidden">{{ full() }}</span>
    </time>
  `,
  styles: `
    :host {
      white-space: nowrap;
    }
  `,
})
export class RelativeTime {
  readonly value = input.required<string | null | undefined>();

  protected readonly iso = computed(() => parseApiDate(this.value())?.toISOString() ?? null);
  protected readonly relative = computed(() => formatRelativeTime(this.value()));
  protected readonly full = computed(() => formatDateTime(this.value()));
}
