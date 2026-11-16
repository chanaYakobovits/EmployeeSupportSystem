import { TicketListScope } from '../../models/ticket.models';

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

/** A queue URL from the navigation state – only a /handler URL is accepted (the state may be anything). */
export function safeQueueUrl(state: unknown): string {
  const url = (state as HandlerNavigationState | null | undefined)?.queueUrl;
  return typeof url === 'string' && url.startsWith(`${HANDLER_HOME_URL}/`) && !url.startsWith(`${HANDLER_HOME_URL}/tickets`)
    ? url
    : HANDLER_HOME_URL;
}
