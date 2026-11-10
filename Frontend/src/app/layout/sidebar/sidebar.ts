import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';

import { NAV_ITEMS } from './nav-items';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, MatListModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav aria-label="תפריט ראשי">
      <mat-nav-list>
        @for (item of items; track item.path) {
          <a
            mat-list-item
            [routerLink]="item.path"
            routerLinkActive
            #rla="routerLinkActive"
            [activated]="rla.isActive"
            [attr.aria-current]="rla.isActive ? 'page' : null"
            (click)="navigate.emit()"
          >
            <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
            <span matListItemTitle>{{ item.label }}</span>
          </a>
        }
      </mat-nav-list>
    </nav>
  `,
})
export class Sidebar {
  protected readonly items = NAV_ITEMS;

  /** Emitted when a link is clicked, so the shell can close the drawer on small screens. */
  readonly navigate = output<void>();
}
