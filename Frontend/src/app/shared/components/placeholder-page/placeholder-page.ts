import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { EmptyState } from '../empty-state/empty-state';

/**
 * Temporary page for an area whose real screens are not built yet.
 * The title comes from the route's `data.title` (bound via withComponentInputBinding).
 */
@Component({
  selector: 'app-placeholder-page',
  imports: [EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <h1 class="page-title">{{ title() }}</h1>
      <app-empty-state icon="construction" message="המסך הזה עדיין בבנייה" />
    </section>
  `,
})
export class PlaceholderPage {
  readonly title = input.required<string>();
}
