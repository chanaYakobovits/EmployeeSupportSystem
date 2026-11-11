import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';

import { AuthService } from '../../core/auth/auth.service';
import { primaryRole, roleLabel } from '../../shared/pipes/role-label.pipe';

/** App header: logo, and the user's avatar / name / role with the user menu. No notifications bell (spec 13.1). */
@Component({
  selector: 'app-header',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatMenuModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Small screens: menu button + avatar only. */
  readonly compact = input(false);
  readonly menuToggle = output<void>();

  private readonly user = this.auth.currentUser;

  protected readonly fullName = computed(() => {
    const user = this.user();
    return user ? `${user.firstName} ${user.lastName}`.trim() : '';
  });

  protected readonly initials = computed(() => {
    const user = this.user();
    if (!user) {
      return '';
    }
    return [user.firstName, user.lastName]
      .map((part) => part.trim().charAt(0))
      .join('')
      .toLocaleUpperCase('he');
  });

  /** Job title in the organization; without one – the most senior system role in Hebrew. */
  protected readonly roleText = computed(() => {
    const user = this.user();
    if (!user) {
      return '';
    }
    return user.jobTitle?.trim() || roleLabel(primaryRole(user.roles));
  });

  protected openChangePassword(): void {
    void this.router.navigate(['/account/change-password'], { queryParams: { returnUrl: this.router.url } });
  }

  protected logout(): void {
    void this.auth.logout();
  }
}
