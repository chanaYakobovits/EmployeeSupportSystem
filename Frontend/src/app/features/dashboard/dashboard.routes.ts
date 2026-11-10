import { Routes } from '@angular/router';

import { PlaceholderPage } from '../../shared/components/placeholder-page/placeholder-page';

// Lazy-loaded area: department / organization dashboards
export const DASHBOARD_ROUTES: Routes = [{ path: '', component: PlaceholderPage, data: { title: 'דשבורד' } }];
