import { PageQuery } from './paging.models';

/** Ticket handling status (spec 3.2, Backend Common.Enums.TicketStatus), in workflow order. */
export type TicketStatus =
  | 'New'
  | 'Assigned'
  | 'InProgress'
  | 'WaitingForEmployee'
  | 'Resolved'
  | 'Closed'
  | 'Cancelled';

export const TICKET_STATUS_VALUES: readonly TicketStatus[] = [
  'New',
  'Assigned',
  'InProgress',
  'WaitingForEmployee',
  'Resolved',
  'Closed',
  'Cancelled',
];

/** Field limits of a ticket (spec 7.4) – same values as the server's TicketFieldLimits. */
export const TICKET_FIELD_LIMITS = {
  subjectMaxLength: 150,
  descriptionMinLength: 10,
  descriptionMaxLength: 4000,
} as const;

/** POST /api/tickets. No requester or department: the server takes them from the signed-in user. */
export interface CreateTicketRequest {
  subject: string | null;
  description: string;
}

/** Response of POST /api/tickets: the ticket number. */
export interface TicketCreated {
  id: number;
}

/** A row of "הפניות שלי" (Backend RequesterTicketListItemDto). The requester never sees urgency. */
export interface RequesterTicketListItem {
  id: number;
  /** The subject, or – when the ticket has none – the start of the description (isSubjectExcerpt). */
  subject: string;
  isSubjectExcerpt: boolean;
  /** Null until the ticket is classified ("מתבצע שיוך..."). */
  categoryName: string | null;
  status: TicketStatus;
  /** Null while no handler is assigned ("טרם שויך"). */
  handlerName: string | null;
  createdAt: string;
  updatedAt: string;
}

/** The summary cards of "הפניות שלי" (spec 7.3). */
export interface RequesterTicketsSummary {
  open: number;
  waitingForMe: number;
  closedThisMonth: number;
}

/** A status change in the requester's timeline (no reason – it may be internal). */
export interface RequesterTicketHistoryItem {
  /** Null for the opening entry. */
  previousStatus: TicketStatus | null;
  newStatus: TicketStatus;
  changedAt: string;
  /** Null when the system made the change. */
  changedByName: string | null;
}

/** GET /api/tickets/{id} for the requester (spec 7.5). */
export interface RequesterTicketDetails {
  id: number;
  subject: string | null;
  description: string;
  status: TicketStatus;
  categoryName: string | null;
  handlerName: string | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  closedAt: string | null;
  timeline: RequesterTicketHistoryItem[];
}

/** Closed list of sortBy values of GET /api/tickets/my (Backend MyTicketSortFields). */
export const MY_TICKET_SORT_FIELDS = ['createdAt', 'updatedAt', 'status'] as const;

/** GET /api/tickets/my. status: any of these (empty / undefined → all). */
export interface MyTicketsQuery extends PageQuery {
  status?: readonly TicketStatus[] | null;
  search?: string | null;
}

/**
 * The status groups of "הפניות שלי" (summary cards and segmented filter) – defined in one place in the client.
 * open = the server's TicketStatuses.RequesterOpen (Resolved counts as open until confirmed); closed includes
 * Cancelled (decision in CLAUDE.md). Every status is in exactly one group.
 */
export type RequesterStatusGroup = 'open' | 'waiting' | 'closed';

export const REQUESTER_STATUS_GROUP_VALUES: readonly RequesterStatusGroup[] = ['open', 'waiting', 'closed'];

export const REQUESTER_STATUS_GROUPS: Readonly<Record<RequesterStatusGroup, readonly TicketStatus[]>> = {
  open: ['New', 'Assigned', 'InProgress', 'Resolved'],
  waiting: ['WaitingForEmployee'],
  closed: ['Closed', 'Cancelled'],
};
