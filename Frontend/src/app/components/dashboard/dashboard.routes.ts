import { Routes } from '@angular/router';
import { BarController, BarElement, CategoryScale, Legend, LineController, LineElement, LinearScale, PointElement, Tooltip } from 'chart.js';
import { provideCharts } from 'ng2-charts';

import { roleGuard } from '../../guards/auth.guards';
import { unsavedChangesGuard } from '../../guards/unsaved-changes.guard';

/**
 * Lazy-loaded area: the dashboards (spec 7.9). Chart.js is provided here and not in app.config, so it is in this
 * area's chunk only – and only the parts the three charts use (bars, lines, the two scales, tooltip, legend).
 */
export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    providers: [
      provideCharts({
        registerables: [
          BarController,
          BarElement,
          LineController,
          LineElement,
          PointElement,
          CategoryScale,
          LinearScale,
          Tooltip,
          Legend,
        ],
        defaults: { font: { family: 'Heebo, sans-serif', size: 13 } },
      }),
    ],
    children: [
      {
        path: '',
        pathMatch: 'full',
        title: 'דשבורד',
        loadComponent: () => import('./dashboard-landing/dashboard-landing-page').then((m) => m.DashboardLandingPage),
      },
      {
        path: 'organization',
        title: 'דשבורד ארגוני',
        canActivate: [roleGuard],
        data: { roles: ['Executive'], dashboard: 'organization' },
        loadComponent: () =>
          import('./organization-dashboard/organization-dashboard-page').then((m) => m.OrganizationDashboardPage),
      },
      {
        // A department manager – their department only (the server answers 404 for any other); the executive – any.
        path: 'departments/:id',
        title: 'דשבורד מחלקתי',
        data: { dashboard: 'department' },
        loadComponent: () =>
          import('./department-dashboard/department-dashboard-page').then((m) => m.DepartmentDashboardPage),
      },
      {
        // The handling screen from a dashboard's table: a manager / executive may not be a Handler (the handler area).
        // The actions are the server's allowedActions – the executive only views. "Back" returns to the dashboard.
        path: 'tickets/:id',
        title: 'פנייה',
        data: { returnArea: 'dashboard' },
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () => import('../handler/ticket-handle/ticket-handle-page').then((m) => m.TicketHandlePage),
      },
    ],
  },
];
