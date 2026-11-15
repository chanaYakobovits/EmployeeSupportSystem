import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/**
 * Something that is not about one field: shown at the top of a form, a dialog or a screen, optionally with a way
 * forward. An error by default; a warning did not block the action (e.g. saved, but no handlers in the department).
 */
export interface FormAlert {
  title: string;
  detail?: string;
  link?: { label: string; url: string };
  tone?: 'error' | 'warning';
}

/** Shows a FormAlert and moves the focus to it, so screen readers announce it (spec 11.6). */
@Component({
  selector: 'app-form-alert',
  imports: [MatButtonModule, MatIconModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'app-alert',
    '[class.app-alert-warning]': 'alert().tone === "warning"',
    role: 'alert',
    tabindex: '-1',
  },
  template: `
    <mat-icon aria-hidden="true">{{ alert().tone === 'warning' ? 'warning' : 'error' }}</mat-icon>
    <div>
      <p class="app-alert-title">{{ alert().title }}</p>
      @if (alert().detail) {
        <p>{{ alert().detail }}</p>
      }
      @if (alert().link; as link) {
        <p><a [routerLink]="link.url">{{ link.label }}</a></p>
      }
    </div>
    @if (dismissible()) {
      <button mat-icon-button type="button" class="app-alert-dismiss" aria-label="סגירת ההודעה" (click)="dismissed.emit()">
        <mat-icon aria-hidden="true">close</mat-icon>
      </button>
    }
  `,
})
export class FormAlertBox {
  readonly alert = input.required<FormAlert>();
  /** Shows a close button (for an alert that stays on a screen, not one inside a form). */
  readonly dismissible = input(false);
  readonly dismissed = output<void>();

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    effect(() => {
      this.alert();
      queueMicrotask(() => host.focus());
    });
  }
}
