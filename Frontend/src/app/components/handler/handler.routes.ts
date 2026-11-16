import { Routes } from '@angular/router';

import { unsavedChangesGuard } from '../../guards/unsaved-changes.guard';
import { QUEUE_TABS } from './handler-urls';

const loadQueuePage = () => import('./ticket-queue/ticket-queue-page').then((m) => m.TicketQueuePage);

/**
 * Lazy-loaded area: handler screens (spec 7.6, 7.7). Each tab of the queue is its own route (data.scope = the API
 * scope), so the tab is in the URL and "clear filters" keeps it. The landing tab is "משויכות אליי".
 */
export const HANDLER_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: QUEUE_TABS[0].path },
  ...QUEUE_TABS.map((tab) => ({
    path: tab.path,
    title: `תור פניות – ${tab.label}`,
    data: { scope: tab.scope },
    loadComponent: loadQueuePage,
  })),
  {
    // :id is bound to TicketHandlePage's id input (withComponentInputBinding).
    path: 'tickets/:id',
    title: 'טיפול בפנייה',
    // Text typed or files chosen in the writing area and not sent.
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./ticket-handle/ticket-handle-page').then((m) => m.TicketHandlePage),
  },
];
