import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { EmptyState } from '../empty-state/empty-state';
import { PageHeader } from '../page-header/page-header';

/**
 * Temporary page for an area whose real screens are not built yet.
 * The title comes from the route's data.title (the area definition), bound via withComponentInputBinding.
 */
@Component({
  selector: 'app-placeholder-page',
  imports: [PageHeader, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './placeholder-page.html',
  styleUrl: './placeholder-page.scss',
})
export class PlaceholderPage {
  readonly title = input.required<string>();
}
