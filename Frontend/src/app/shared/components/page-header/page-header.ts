import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Uniform page header (design guide): title + subtitle on the right, main action on the left.
 * Project the action with the pageActions attribute: <button pageActions mat-flat-button>…</button>
 */
@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './page-header.html',
  styleUrl: './page-header.scss',
})
export class PageHeader {
  readonly heading = input.required<string>();
  readonly subtitle = input<string>();
}
