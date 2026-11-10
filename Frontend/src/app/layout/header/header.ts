import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';

@Component({
  selector: 'app-header',
  imports: [RouterLink, MatToolbarModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-toolbar class="header">
      @if (showMenuButton()) {
        <button mat-icon-button type="button" aria-label="פתיחת תפריט" (click)="menuToggle.emit()">
          <mat-icon>menu</mat-icon>
        </button>
      }
      <a class="brand" routerLink="/">עובדנט</a>
      <span class="spacer"></span>
      <!-- Task 2.2: user name + logout -->
    </mat-toolbar>
  `,
  styles: `
    .header {
      gap: 8px;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
    }
    .header button {
      color: inherit;
    }
    .brand {
      color: inherit;
      text-decoration: none;
      font: var(--mat-sys-title-large);
    }
    .spacer {
      flex: 1;
    }
  `,
})
export class Header {
  readonly showMenuButton = input(false);
  readonly menuToggle = output<void>();
}
