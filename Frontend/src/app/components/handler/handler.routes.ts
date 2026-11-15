import { Routes } from '@angular/router';

import { PlaceholderPage } from '../shared/placeholder-page/placeholder-page';

// Lazy-loaded area: handler screens (ticket queue, ticket handling)
export const HANDLER_ROUTES: Routes = [{ path: '', component: PlaceholderPage, data: { title: 'תור פניות' } }];
