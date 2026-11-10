import { Routes } from '@angular/router';

import { DEV_AREA_ROUTES } from './features/dev/dev-area';
import { NotFound } from './layout/not-found/not-found';
import { Shell } from './layout/shell/shell';

export const routes: Routes = [
  // Task 2.2: 'login' / 'change-password' (outside the shell) + guards.
  {
    path: '',
    component: Shell,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'employee' },
      {
        path: 'employee',
        loadChildren: () => import('./features/employee/employee.routes').then((m) => m.EMPLOYEE_ROUTES),
      },
      {
        path: 'handler',
        loadChildren: () => import('./features/handler/handler.routes').then((m) => m.HANDLER_ROUTES),
      },
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'admin',
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      // Development-only area; replaced by an empty list in the production build.
      ...DEV_AREA_ROUTES,
      { path: '**', component: NotFound },
    ],
  },
];
