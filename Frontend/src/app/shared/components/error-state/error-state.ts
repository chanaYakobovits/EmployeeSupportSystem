import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/** "Error" view state with a retry button (spec 7.10). */
@Component({
  selector: 'app-error-state',
  imports: [MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="state" role="alert">
      <span class="state-icon-wrap error"><mat-icon class="state-icon" aria-hidden="true">error</mat-icon></span>
      <h2 class="state-title">{{ heading() }}</h2>
      @if (description()) {
        <p class="state-description">{{ description() }}</p>
      }
      <div class="state-actions">
        <button mat-stroked-button type="button" (click)="retry.emit()">
          <mat-icon aria-hidden="true">refresh</mat-icon>
          נסה שוב
        </button>
      </div>
    </div>
  `,
  styleUrl: '../view-state.scss',
})
export class ErrorState {
  readonly heading = input('לא הצלחנו לטעון את הנתונים');
  readonly description = input<string>('ייתכן שמדובר בתקלה זמנית. אפשר לנסות שוב.');
  readonly retry = output<void>();
}
