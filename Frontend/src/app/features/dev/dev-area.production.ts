import { Routes } from '@angular/router';

import type { NavItem } from '../../layout/sidebar/nav-items';

// Production replacement for dev-area.ts: no dev routes, no dev menu items.
export const DEV_AREA_ROUTES: Routes = [];

export const DEV_AREA_NAV_ITEMS: readonly NavItem[] = [];
