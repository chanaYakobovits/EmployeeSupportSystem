import { Routes } from '@angular/router';

import { PlaceholderPage } from '../../shared/components/placeholder-page/placeholder-page';

// Lazy-loaded area: administration (users, departments, categories, SLA)
export const ADMIN_ROUTES: Routes = [{ path: '', component: PlaceholderPage, data: { title: 'ניהול' } }];
