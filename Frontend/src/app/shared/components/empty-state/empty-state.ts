import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * "Empty" view state (spec 7.10). Project an action button as content, e.g.
 * <app-empty-state message="אין לך פניות עדיין"><button mat-flat-button>פנייה חדשה</button></app-empty-state>
 */
@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="state">
      <mat-icon class="state-icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <p>{{ message() }}</p>
      <ng-content />
    </div>
  `,
  styleUrl: '../view-state.scss',
})
export class EmptyState {
  readonly message = input.required<string>();
  readonly icon = input('inbox');
}
