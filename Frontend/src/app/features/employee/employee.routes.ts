import { Routes } from '@angular/router';

import { unsavedChangesGuard } from '../../shared/forms/unsaved-changes.guard';

/** Lazy-loaded area: employee screens (spec 7.3–7.5). Every screen is lazy too. */
export const EMPLOYEE_ROUTES: Routes = [
  {
    path: '',
    title: 'הפניות שלי',
    loadComponent: () => import('./my-tickets/my-tickets-page').then((m) => m.MyTicketsPage),
  },
  {
    path: 'tickets/new',
    title: 'פנייה חדשה',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./new-ticket/new-ticket-page').then((m) => m.NewTicketPage),
  },
  {
    // :id is bound to TicketViewPage's id input (withComponentInputBinding).
    path: 'tickets/:id',
    title: 'צפייה בפנייה',
    loadComponent: () => import('./ticket-view/ticket-view-page').then((m) => m.TicketViewPage),
  },
];
