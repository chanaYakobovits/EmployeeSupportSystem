export const MY_TICKETS_URL = '/employee';
export const NEW_TICKET_URL = '/employee/tickets/new';

/** The view screen of a ticket. */
export function ticketUrl(id: number): string {
  return `/employee/tickets/${id}`;
}
