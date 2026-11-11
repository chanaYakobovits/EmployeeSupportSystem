import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

/** "Loading" view state (spec 7.10). */
@Component({
  selector: 'app-loading-state',
  imports: [MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="state" role="status" aria-live="polite">
      <mat-spinner diameter="40" aria-hidden="true" />
      <p class="state-description">{{ message() }}</p>
    </div>
  `,
  styleUrl: '../view-state.scss',
})
export class LoadingState {
  readonly message = input('טוען...');
}
