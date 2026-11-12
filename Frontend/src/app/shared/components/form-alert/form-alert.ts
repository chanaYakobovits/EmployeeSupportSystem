import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/** An error that is not about one field: shown at the top of a form or a dialog, optionally with a way forward. */
export interface FormAlert {
  title: string;
  detail?: string;
  link?: { label: string; url: string };
}

/** Shows a FormAlert and moves the focus to it, so screen readers announce it (spec 11.6). */
@Component({
  selector: 'app-form-alert',
  imports: [MatIconModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'app-alert', role: 'alert', tabindex: '-1' },
  template: `
    <mat-icon aria-hidden="true">error</mat-icon>
    <div>
      <p class="app-alert-title">{{ alert().title }}</p>
      @if (alert().detail) {
        <p>{{ alert().detail }}</p>
      }
      @if (alert().link; as link) {
        <p><a [routerLink]="link.url">{{ link.label }}</a></p>
      }
    </div>
  `,
})
export class FormAlertBox {
  readonly alert = input.required<FormAlert>();

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    effect(() => {
      this.alert();
      queueMicrotask(() => host.focus());
    });
  }
}
