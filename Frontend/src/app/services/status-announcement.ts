import { TicketStatus } from '../models/ticket.models';
import { statusLabel } from '../pipes/status-label.pipe';

/**
 * The screen-reader announcement when the shown ticket was loaded again with another status (spec 11.6): after an
 * action, a reload after 409, or an automatic move (the requester's reply). Null on the first load or another ticket.
 */
export function statusChangeAnnouncement(
  previous: { id: number; status: TicketStatus } | null,
  next: { id: number; status: TicketStatus },
): string | null {
  return previous !== null && previous.id === next.id && previous.status !== next.status
    ? `סטטוס הפנייה עודכן: ${statusLabel(next.status)}`
    : null;
}
