import { Routes } from '@angular/router';

// TEMPORARY – development-only tools. Registered in app.routes.ts only when !environment.production.
export const DEV_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'errors' },
  {
    path: 'errors',
    loadComponent: () => import('./dev-errors/dev-errors-page').then((m) => m.DevErrorsPage),
  },
];
