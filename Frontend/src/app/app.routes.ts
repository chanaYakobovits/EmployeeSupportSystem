import { Routes } from '@angular/router';

import { authGuard, changePasswordGuard, guestGuard, mustChangePasswordGuard } from './core/auth/guards/auth.guards';
import { APP_AREAS, AppArea, areaRoute } from './core/navigation/app-areas';
import { DEV_AREAS, loadDevRoutes } from './features/dev/dev-area';

const [EMPLOYEE, HANDLER, DASHBOARD, ADMIN] = APP_AREAS;

const loadChangePasswordPage = () =>
  import('./features/auth/change-password/change-password-page').then((m) => m.ChangePasswordPage);

// Every routed screen is lazy (loadComponent / loadChildren) to keep the initial bundle small.
// Area routes (path, roles, title) come from core/navigation/app-areas.ts – the same definition as the menu.
export const routes: Routes = [
  // Auth screens live outside the shell (spec 7.1, 7.2).
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login-page').then((m) => m.LoginPage),
  },
  {
    // First login: mandatory, no shell, no cancel.
    path: 'change-password',
    canActivate: [authGuard, changePasswordGuard],
    loadComponent: loadChangePasswordPage,
  },
  {
    path: '',
    canActivate: [authGuard, mustChangePasswordGuard],
    loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: EMPLOYEE.path },
      areaRoute(EMPLOYEE, () => import('./features/employee/employee.routes').then((m) => m.EMPLOYEE_ROUTES)),
      areaRoute(HANDLER, () => import('./features/handler/handler.routes').then((m) => m.HANDLER_ROUTES)),
      areaRoute(DASHBOARD, () => import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES)),
      areaRoute(ADMIN, () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES)),
      // Development-only area; DEV_AREAS is empty in the production build.
      ...DEV_AREAS.map((area: AppArea) => areaRoute(area, loadDevRoutes)),
      {
        // Voluntary change from the user menu: inside the shell, with cancel.
        path: 'account/change-password',
        data: { mode: 'voluntary' },
        loadComponent: loadChangePasswordPage,
      },
      { path: '**', loadComponent: () => import('./layout/not-found/not-found').then((m) => m.NotFound) },
    ],
  },
];
