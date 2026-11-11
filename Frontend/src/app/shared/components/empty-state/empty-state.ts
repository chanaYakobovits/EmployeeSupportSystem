import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * "Empty" view state (spec 7.10): grey icon, title, explanation and an optional projected action, e.g.
 * <app-empty-state heading="אין לך פניות עדיין" description="…"><a mat-flat-button>פנייה חדשה</a></app-empty-state>
 */
@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="state">
      <span class="state-icon-wrap"><mat-icon class="state-icon" aria-hidden="true">{{ icon() }}</mat-icon></span>
      <h2 class="state-title">{{ heading() }}</h2>
      @if (description()) {
        <p class="state-description">{{ description() }}</p>
      }
      <div class="state-actions"><ng-content /></div>
    </div>
  `,
  styleUrl: '../view-state.scss',
})
export class EmptyState {
  readonly heading = input.required<string>();
  readonly description = input<string>();
  readonly icon = input('inbox');
}
