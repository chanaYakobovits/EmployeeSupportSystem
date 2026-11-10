import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

import { EmptyState } from '../../shared/components/empty-state/empty-state';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink, MatButtonModule, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-empty-state icon="search_off" message="הדף לא נמצא">
        <a mat-flat-button routerLink="/">חזרה לדף הבית</a>
      </app-empty-state>
    </section>
  `,
})
export class NotFound {}
