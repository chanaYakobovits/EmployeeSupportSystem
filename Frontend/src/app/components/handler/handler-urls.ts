import { Role } from '../../models/auth.models';
import { TicketListScope } from '../../models/ticket.models';
import { DASHBOARD_HOME_URL, dashboardTicketUrl } from '../dashboard/dashboard-urls';

/** The handler's landing page: the default tab of the queue. */
export const HANDLER_HOME_URL = '/handler';

/** The tabs of the queue (spec 7.6), in display order. Each tab is a route and a scope of GET /api/tickets. */
export interface QueueTab {
  scope: Extract<TicketListScope, 'assigned' | 'queue' | 'handling'>;
  /** Route path under /handler. */
  path: string;
  label: string;
}

export const QUEUE_TABS: readonly QueueTab[] = [
  { scope: 'assigned', path: 'assigned', label: 'משויכות אליי' },
  { scope: 'queue', path: 'queue', label: 'תור המחלקה (לא משויכות)' },
  { scope: 'handling', path: 'handling', label: 'כל פניות המחלקה המטפלת' },
];

export function queueTabUrl(tab: QueueTab): string {
  return `${HANDLER_HOME_URL}/${tab.path}`;
}

/** The handling screen of a ticket (spec 7.7). */
export function handlerTicketUrl(id: number): string {
  return `${HANDLER_HOME_URL}/tickets/${id}`;
}

/**
 * Navigation state from the queue to the handling screen: the list's URL (tab + filters), so "back to the queue" and
 * the return after a transfer land on the same view.
 */
export interface HandlerNavigationState {
  queueUrl?: string;
}

/**
 * The areas the handling screen is opened from (route data "returnArea"): the queue, or a dashboard (task 5.2 – a
 * manager / executive may not have the Handler role). Each with its home and its "back" text.
 */
export type ReturnArea = 'handler' | 'dashboard';

export const RETURN_AREAS: Readonly<Record<ReturnArea, { home: string; label: string }>> = {
  handler: { home: HANDLER_HOME_URL, label: 'חזרה לתור הפניות' },
  dashboard: { home: DASHBOARD_HOME_URL, label: 'חזרה לדשבורד' },
};

/**
 * The URL to go back to, from the navigation state – only a URL of the area itself, and not another ticket, is
 * accepted (the state may be anything); otherwise the area's home.
 */
export function safeQueueUrl(state: unknown, area: ReturnArea = 'handler'): string {
  const home = RETURN_AREAS[area].home;
  const url = (state as HandlerNavigationState | null | undefined)?.queueUrl;
  return typeof url === 'string' && url.startsWith(`${home}/`) && !url.startsWith(`${home}/tickets`) ? url : home;
}

/**
 * The handling screen of a ticket for a staff member: a Handler's is in the handler area; a department manager's or an
 * executive's (who may not have the Handler role) – in the dashboard area. null = no staff screen for these roles.
 */
export function staffTicketUrl(id: number, roles: readonly Role[]): string | null {
  if (roles.includes('Handler')) {
    return handlerTicketUrl(id);
  }
  return roles.includes('DepartmentManager') || roles.includes('Executive') ? dashboardTicketUrl(id) : null;
}
