import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { MatSidenavModule } from '@angular/material/sidenav';
import { map } from 'rxjs';

import { Header } from '../header/header';
import { Sidebar } from '../sidebar/sidebar';

/** Application frame: header + sidebar + routed content. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, MatSidenavModule, Header, Sidebar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly breakpoints = inject(BreakpointObserver);

  /** Small screens: the sidebar is an overlay drawer opened from the header. */
  protected readonly isHandset = toSignal(
    this.breakpoints.observe([Breakpoints.XSmall, Breakpoints.Small]).pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  private readonly drawerOpenOnHandset = signal(false);

  protected readonly sidenavMode = computed(() => (this.isHandset() ? 'over' : 'side'));
  protected readonly sidenavOpened = computed(() => !this.isHandset() || this.drawerOpenOnHandset());

  protected toggleDrawer(): void {
    this.drawerOpenOnHandset.update((open) => !open);
  }

  protected closeDrawerOnHandset(): void {
    if (this.isHandset()) {
      this.drawerOpenOnHandset.set(false);
    }
  }
}
