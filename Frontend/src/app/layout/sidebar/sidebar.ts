import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';

import { AuthService } from '../../core/auth/auth.service';
import { visibleAreas } from '../../core/navigation/app-areas';

/** Main menu: only the areas the user's roles allow (spec 7), in a fixed order. */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, MatListModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss',
})
export class Sidebar {
  private readonly auth = inject(AuthService);

  protected readonly items = computed(() => visibleAreas(this.auth.roles()));

  /** Emitted when a link is clicked, so the shell can close the drawer on small screens. */
  readonly navigate = output<void>();
}
