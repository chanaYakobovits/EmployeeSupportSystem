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
      <mat-icon class="state-icon state-icon-error" aria-hidden="true">error</mat-icon>
      <p>{{ message() }}</p>
      <button mat-stroked-button type="button" (click)="retry.emit()">
        <mat-icon>refresh</mat-icon>
        נסה שוב
      </button>
    </div>
  `,
  styleUrl: '../view-state.scss',
})
export class ErrorState {
  readonly message = input('לא הצלחנו לטעון את הנתונים');
  readonly retry = output<void>();
}
