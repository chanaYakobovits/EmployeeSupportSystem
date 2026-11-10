import { Routes } from '@angular/router';

import { PlaceholderPage } from '../../shared/components/placeholder-page/placeholder-page';

// Lazy-loaded area: employee screens (my tickets, new ticket, ticket view)
export const EMPLOYEE_ROUTES: Routes = [{ path: '', component: PlaceholderPage, data: { title: 'הפניות שלי' } }];
