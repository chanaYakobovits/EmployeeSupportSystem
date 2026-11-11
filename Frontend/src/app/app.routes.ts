import { Routes } from '@angular/router';

import {
  authGuard,
  changePasswordGuard,
  guestGuard,
  mustChangePasswordGuard,
  roleGuard,
} from './core/auth/guards/auth.guards';
import { Role } from './core/models/auth.models';
import { DEV_AREA_ROUTES } from './features/dev/dev-area';

const HANDLER_ROLES: readonly Role[] = ['Handler'];
const DASHBOARD_ROLES: readonly Role[] = ['DepartmentManager', 'Executive'];
const ADMIN_ROLES: readonly Role[] = ['Admin'];

// Every routed screen is lazy (loadComponent / loadChildren) to keep the initial bundle small.
export const routes: Routes = [
  // Auth screens live outside the shell (spec 7.1, 7.2).
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'change-password',
    canActivate: [authGuard, changePasswordGuard],
    loadComponent: () =>
      import('./features/auth/change-password/change-password-page').then((m) => m.ChangePasswordPage),
  },
  {
    path: '',
    canActivate: [authGuard, mustChangePasswordGuard],
    loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'employee' },
      {
        path: 'employee',
        loadChildren: () => import('./features/employee/employee.routes').then((m) => m.EMPLOYEE_ROUTES),
      },
      {
        path: 'handler',
        canActivate: [roleGuard],
        data: { roles: HANDLER_ROLES },
        loadChildren: () => import('./features/handler/handler.routes').then((m) => m.HANDLER_ROUTES),
      },
      {
        path: 'dashboard',
        canActivate: [roleGuard],
        data: { roles: DASHBOARD_ROLES },
        loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'admin',
        canActivate: [roleGuard],
        data: { roles: ADMIN_ROLES },
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      // Development-only area; replaced by an empty list in the production build.
      ...DEV_AREA_ROUTES,
      { path: '**', loadComponent: () => import('./layout/not-found/not-found').then((m) => m.NotFound) },
    ],
  },
];
