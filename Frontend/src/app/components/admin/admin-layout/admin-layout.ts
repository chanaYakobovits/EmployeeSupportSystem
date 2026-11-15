import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface AdminTab {
  path: string;
  label: string;
  icon: string;
}

/** The administration area (spec 7.8): tabs + the selected screen. */
@Component({
  selector: 'app-admin-layout',
  imports: [MatTabsModule, MatIconModule, RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-layout.html',
  styleUrl: './admin-layout.scss',
})
export class AdminLayout {
  protected readonly tabs: readonly AdminTab[] = [
    { path: 'users', label: 'עובדים', icon: 'group' },
    { path: 'departments', label: 'מחלקות', icon: 'apartment' },
    { path: 'categories', label: 'קטגוריות', icon: 'category' },
    { path: 'sla', label: 'SLA', icon: 'timer' },
  ];
}
