import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { DashboardRange } from '../../../models/dashboard.models';
import { formatDay } from '../dashboard-format';
import { DashboardRangeChoice, PRESET_DAYS, PresetDays, customRangeError, israelToday } from '../dashboard-range';

type Segment = `${PresetDays}` | 'custom';

/**
 * The range of a dashboard (spec 7.9): 7 / 30 / 90 days or a custom range (two native date inputs, as in the queue).
 * A preset applies at once; a custom range on "החל", after the same checks as the server. Shows the days the numbers
 * cover (the server's resolved range).
 */
@Component({
  selector: 'app-dashboard-range-picker',
  imports: [MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-range-picker.html',
  styleUrl: './dashboard-range-picker.scss',
})
export class DashboardRangePicker {
  readonly range = input.required<DashboardRangeChoice>();
  /** The days the shown numbers cover (from the response); null while loading. */
  readonly resolved = input<DashboardRange | null>(null);
  readonly rangeChange = output<DashboardRangeChoice>();

  protected readonly presets = PRESET_DAYS;
  protected readonly today = israelToday();
  /** "מותאם" chosen but not applied yet. */
  protected readonly customOpen = signal(false);
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly error = signal<string | null>(null);

  protected readonly segment = computed<Segment>(() => {
    const range = this.range();
    return this.customOpen() || range.kind === 'custom' ? 'custom' : (`${range.days}` as Segment);
  });

  protected readonly resolvedText = computed(() => {
    const resolved = this.resolved();
    return resolved ? `${formatDay(resolved.from)} – ${formatDay(resolved.to)}` : '';
  });

  constructor() {
    // The URL changed (back, another tab): the fields follow it.
    effect(() => {
      const range = this.range();
      untracked(() => {
        if (range.kind === 'custom') {
          this.from.set(range.from);
          this.to.set(range.to);
        } else {
          this.customOpen.set(false);
        }
        this.error.set(null);
      });
    });
  }

  protected onSegment(value: Segment): void {
    if (value === 'custom') {
      this.customOpen.set(true);
      const resolved = this.resolved();
      if (!this.from() && resolved) {
        this.from.set(resolved.from);
        this.to.set(resolved.to);
      }
      return;
    }
    this.customOpen.set(false);
    this.error.set(null);
    this.rangeChange.emit({ kind: 'preset', days: Number(value) as PresetDays });
  }

  protected apply(event: Event): void {
    event.preventDefault();
    const from = this.from();
    const to = this.to();
    const error = customRangeError(from, to);
    this.error.set(error);
    if (error === null) {
      this.rangeChange.emit({ kind: 'custom', from, to });
    }
  }
}
