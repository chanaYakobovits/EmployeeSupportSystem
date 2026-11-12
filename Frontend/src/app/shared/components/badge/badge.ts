import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type BadgeTone = 'primary' | 'accent' | 'success' | 'warning' | 'danger' | 'neutral';

/** "Pill" tag (design guide): a 10% tint of the colour behind text in the full colour. <app-badge tone="success">פעיל</app-badge> */
@Component({
  selector: 'app-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': '"badge tone-" + tone()' },
  template: `<ng-content />`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      padding: 2px 10px;
      border-radius: 20px;
      font-size: 12px;
      font-weight: 600;
      line-height: 20px;
      white-space: nowrap;
    }
    :host(.tone-primary) {
      background: var(--primary-soft);
      color: var(--primary);
    }
    :host(.tone-accent) {
      background: var(--accent-soft);
      color: var(--accent-text);
    }
    :host(.tone-success) {
      background: var(--success-soft);
      color: var(--success-text);
    }
    :host(.tone-warning) {
      background: var(--warning-soft);
      color: var(--warning-text);
    }
    :host(.tone-danger) {
      background: var(--error-bg);
      color: var(--error-text);
    }
    :host(.tone-neutral) {
      background: var(--neutral-soft);
      color: var(--text-secondary);
    }
  `,
})
export class Badge {
  readonly tone = input<BadgeTone>('neutral');
}
