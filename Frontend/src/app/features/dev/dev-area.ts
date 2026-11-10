import { Routes } from '@angular/router';

import type { NavItem } from '../../layout/sidebar/nav-items';

// TEMPORARY – development-only area. In the production build this file is replaced by
// dev-area.production.ts (angular.json fileReplacements), so none of the dev code is bundled.

export const DEV_AREA_ROUTES: Routes = [
  { path: 'dev', loadChildren: () => import('./dev.routes').then((m) => m.DEV_ROUTES) },
];

export const DEV_AREA_NAV_ITEMS: readonly NavItem[] = [
  { label: 'בדיקות (פיתוח)', icon: 'bug_report', path: '/dev' },
];
