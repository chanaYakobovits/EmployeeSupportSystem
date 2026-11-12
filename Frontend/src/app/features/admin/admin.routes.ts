import { Routes } from '@angular/router';

/**
 * Lazy-loaded area: administration (spec 7.8), Admin only (guarded by the area route).
 * Every screen is lazy too. Categories and SLA are added in task 2.4.
 */
export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./admin-layout/admin-layout').then((m) => m.AdminLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'users' },
      {
        path: 'users',
        loadComponent: () => import('./users/users-list/users-list').then((m) => m.UsersList),
      },
      {
        path: 'users/new',
        loadComponent: () => import('./users/user-form/user-form').then((m) => m.UserForm),
      },
      {
        // :id is bound to UserForm's id input (withComponentInputBinding).
        path: 'users/:id',
        loadComponent: () => import('./users/user-form/user-form').then((m) => m.UserForm),
      },
      {
        path: 'departments',
        loadComponent: () => import('./departments/departments-page/departments-page').then((m) => m.DepartmentsPage),
      },
    ],
  },
];
