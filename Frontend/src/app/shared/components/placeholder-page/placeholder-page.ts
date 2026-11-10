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
  templateUrl: './placeholder-page.html',
  styleUrl: './placeholder-page.scss',
})
export class PlaceholderPage {
  readonly title = input.required<string>();
}
