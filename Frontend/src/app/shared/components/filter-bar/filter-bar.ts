import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Filter bar above a list (design guide): search, filters and segmented buttons in one row; a column on mobile.
 * Layout only – the controls are projected:
 * <app-filter-bar label="סינון עובדים"><app-search-field class="grow" … /><mat-form-field …/></app-filter-bar>
 * A projected element with class "grow" takes the remaining width.
 */
@Component({
  selector: 'app-filter-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'search', '[attr.aria-label]': 'label()' },
  template: `<ng-content />`,
  styles: `
    :host {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
      padding: 16px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius-card);
      box-shadow: var(--shadow-sm);
    }
    :host > ::ng-deep .grow {
      flex: 1 1 260px;
      min-width: 0;
    }
    @media (max-width: 767.98px) {
      :host {
        flex-direction: column;
        align-items: stretch;
      }
      :host > ::ng-deep * {
        width: 100%;
      }
    }
  `,
})
export class FilterBar {
  readonly label = input('סינון');
}
