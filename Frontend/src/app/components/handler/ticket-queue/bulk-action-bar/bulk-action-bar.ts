import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { BULK_LIMITS, BulkAction } from '../../../../models/ticket.models';

/**
 * The bar of the bulk actions (task 3.6), shown while tickets are selected: "נבחרו X פניות" (announced politely),
 * "התחל טיפול", "העברה…" and "ניקוי בחירה". Over the limit the actions are disabled and the limit is explained.
 * Presentational: the queue page runs the actions.
 */
@Component({
  selector: 'app-bulk-action-bar',
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bulk-action-bar.html',
  styleUrl: './bulk-action-bar.scss',
})
export class BulkActionBar {
  readonly count = input.required<number>();
  /** The action that is running, or null. Every button is locked while one runs. */
  readonly running = input<BulkAction | null>(null);

  readonly start = output<void>();
  readonly assign = output<void>();
  readonly clear = output<void>();

  protected readonly maxItems = BULK_LIMITS.maxItems;
  protected readonly overLimit = computed(() => this.count() > BULK_LIMITS.maxItems);
  protected readonly busy = computed(() => this.running() !== null);
  protected readonly countText = computed(() =>
    this.count() === 1 ? 'נבחרה פנייה אחת' : `נבחרו ${this.count()} פניות`,
  );
}
