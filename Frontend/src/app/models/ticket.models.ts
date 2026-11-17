import { TicketAttachment } from './attachment.models';
import { Urgency } from './sla.models';
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

/** Limits of a comment – same value as the server's CommentFieldLimits (counted after trimming). */
export const COMMENT_FIELD_LIMITS = {
  bodyMaxLength: 4000,
} as const;

/** POST /api/tickets. No requester or department: the server takes them from the signed-in user. */
export interface CreateTicketRequest {
  subject: string | null;
  description: string;
  /** Up to 3 files (ATTACHMENT_LIMITS), already checked by validateFiles. */
  files: readonly File[];
}

/** Response of POST /api/tickets/{id}/comments. */
export interface CommentCreated {
  id: number;
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
  /** Status changes only (task 3.1). The ticket view shows GET /history instead. */
  timeline: RequesterTicketHistoryItem[];
  /** Base64; sent back with the requester's status changes (cancel, confirm, reopen). */
  rowVersion: string;
  /** What the requester may do now – the screen shows only these (never computed in the client). */
  allowedActions: RequesterTicketAction[];
}

/** The requester's actions on their ticket (Backend TicketActions: reply, cancel, confirmResolved, reopen). */
export type RequesterTicketAction = 'reply' | 'cancel' | 'confirmResolved' | 'reopen';

/**
 * After a resolution the requester may reopen within this many days; then the ticket is closed automatically.
 * Same value as the server's TicketStatusRules.ResolvedWindow – change both together.
 */
export const RESOLVED_WINDOW_DAYS = 7;

/**
 * PATCH /api/tickets/{id}/status (JSON). message: a comment the requester sees, saved with the change – required for
 * WaitingForEmployee and Resolved. reason: internal (handlers only). Files go through POST /attachments afterwards.
 */
export interface ChangeTicketStatusRequest {
  newStatus: TicketStatus;
  rowVersion: string;
  reason?: string;
  message?: string;
}

/** A status change in the timeline of GET /history. No reason (it may be internal). */
export interface RequesterTimelineStatus {
  kind: 'status';
  at: string;
  /** Null for the opening entry. */
  previousStatus: TicketStatus | null;
  newStatus: TicketStatus;
  /** Null when the system made the change. */
  changedByName: string | null;
}

/** A comment visible to the requester, with its files. */
export interface RequesterTimelineComment {
  kind: 'comment';
  at: string;
  id: number;
  authorName: string;
  /** Written by the requester – shown on their side of the conversation. */
  isFromRequester: boolean;
  body: string;
  /** The handler's resolution reply (task 3.5). */
  isResolution: boolean;
  attachments: TicketAttachment[];
}

/** A file attached without a comment (including the files of the new-ticket form). */
export interface RequesterTimelineAttachment {
  kind: 'attachment';
  at: string;
  attachment: TicketAttachment;
}

export type RequesterTimelineItem = RequesterTimelineStatus | RequesterTimelineComment | RequesterTimelineAttachment;

/**
 * GET /api/tickets/{id}/history for the requester (Backend RequesterTicketHistoryDto): oldest first. Internal notes and
 * reasons are left out by the server – the client shows whatever it gets.
 */
export interface RequesterTicketHistory {
  ticketId: number;
  items: RequesterTimelineItem[];
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

// ===================== Staff (task 3.4) =====================

/** Statuses in which the SLA clock runs – same as the server's TicketStatuses.Open (WaitingForEmployee: paused). */
export const OPEN_SLA_STATUSES: readonly TicketStatus[] = ['New', 'Assigned', 'InProgress'];

/** Classification and assignment are possible – not once resolved (the server's EnsureOpen). */
export const ACTIVE_TICKET_STATUSES: readonly TicketStatus[] = ['New', 'Assigned', 'InProgress', 'WaitingForEmployee'];

/** Who set the category (Backend ClassificationSource). */
export type ClassificationSource = 'Ai' | 'Fallback' | 'Manual';

/** The staff lists of GET /api/tickets (Backend TicketListScope). The handler's tabs are assigned / queue / handling. */
export type TicketListScope = 'assigned' | 'queue' | 'handling' | 'department';

/** An urgency filter value; NotSet = "לא נקבעה" (Backend UrgencyFilter). */
export type UrgencyFilter = Urgency | 'NotSet';

export const URGENCY_FILTER_VALUES: readonly UrgencyFilter[] = ['Critical', 'High', 'Medium', 'Low', 'NotSet'];

/**
 * Closed and cancelled: the staff lists leave them out unless the status filter asks for them
 * (Backend TicketStatuses.Final).
 */
export const FINAL_TICKET_STATUSES: readonly TicketStatus[] = ['Closed', 'Cancelled'];

/**
 * Closed list of sortBy values of GET /api/tickets (Backend StaffTicketSortFields). priority is the default: waiting
 * for the handler first (overdue, then the most urgent, then the nearest due time), then waiting for the employee,
 * resolved, closed – those by the latest update. It has one fixed order (sortDir is ignored).
 */
export const STAFF_TICKET_SORT_FIELDS = ['priority', 'dueAt', 'urgency', 'createdAt', 'updatedAt', 'status'] as const;

export type StaffTicketSortField = (typeof STAFF_TICKET_SORT_FIELDS)[number];

/** The options of the queue's sort select. */
export const STAFF_TICKET_SORT_LABELS: Record<StaffTicketSortField, string> = {
  priority: 'עדיפות',
  dueAt: 'מועד יעד',
  urgency: 'דחיפות',
  createdAt: 'תאריך פתיחה',
  updatedAt: 'עדכון אחרון',
  status: 'סטטוס',
};

/** Without sortDir the server sorts these descending (StaffTicketSortFields.IsDescendingByDefault). */
export const STAFF_TICKET_DESCENDING_BY_DEFAULT: readonly string[] = ['urgency', 'createdAt', 'updatedAt'];

/** GET /api/tickets. Dates are calendar days (yyyy-MM-dd, Israel time), both included. */
export interface StaffTicketsQuery extends PageQuery {
  scope: TicketListScope;
  status?: readonly TicketStatus[] | null;
  categoryId?: number | null;
  urgency?: readonly UrgencyFilter[] | null;
  from?: string | null;
  to?: string | null;
  overdueOnly?: boolean | null;
  search?: string | null;
}

/** A row of the staff lists (Backend StaffTicketListItemDto). */
export interface StaffTicketListItem {
  id: number;
  subject: string;
  isSubjectExcerpt: boolean;
  requesterName: string;
  /** The requester's department when the ticket was opened. */
  requesterDepartmentName: string;
  categoryName: string | null;
  /** Null until set; its SLA is then Medium. */
  urgency: Urgency | null;
  status: TicketStatus;
  handlerName: string | null;
  assignedDepartmentName: string | null;
  dueAt: string | null;
  /** Computed by the server when the list was loaded. */
  isOverdue: boolean;
  classificationSource: ClassificationSource | null;
  createdAt: string;
  updatedAt: string;
  /** Base64 – sent back with "קח לטיפולי" (and the bulk actions of task 3.6). */
  rowVersion: string;
}

/** The actions the server allows on one ticket (Backend TicketActions) – the screen shows only these. */
export type TicketAction =
  | 'reply'
  | 'internalNote'
  | 'reclassify'
  | 'assign'
  | 'transferToDepartment'
  | 'takeFromQueue'
  /** "התחל טיפול": Assigned → InProgress (from the queue it also assigns the user). */
  | 'start'
  /** InProgress → WaitingForEmployee, with a message to the employee. */
  | 'requestInfo'
  /** InProgress → Resolved, with the resolution reply. */
  | 'resolve'
  /** The AI analysis area (task 4.4): staff with a scope. */
  | 'viewAiAnalysis'
  /** "נתח מחדש": a handler, an open ticket, no analysis in progress. */
  | 'retryAnalysis'
  /** "החל המלצה": reclassify allowed, and the active recommendation differs from the current classification. */
  | 'applyRecommendation';

export interface NamedRef {
  id: number;
  name: string;
}

/** GET /api/tickets/{id} for staff (Backend StaffTicketDetailsDto). */
export interface StaffTicketDetails {
  id: number;
  subject: string | null;
  description: string;
  status: TicketStatus;
  requester: { id: number; name: string; departmentName: string; jobTitle: string };
  category: NamedRef | null;
  urgency: Urgency | null;
  classificationSource: ClassificationSource | null;
  dueAt: string | null;
  isOverdue: boolean;
  assignedTo: NamedRef | null;
  assignedDepartment: NamedRef | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  closedAt: string | null;
  /** Base64; sent back with every change (spec 11.2). */
  rowVersion: string;
  allowedActions: TicketAction[];
}

/** GET /api/tickets/{id}: the requester's view (also for staff who opened the ticket) or the staff view. */
export type TicketDetails = RequesterTicketDetails | StaffTicketDetails;

/**
 * By the requester object, which only the staff view has. Not by allowedActions: since task 3.5 the requester's view
 * carries its own actions (cancel, confirm, reopen) and rowVersion too.
 */
export function isStaffTicketDetails(ticket: TicketDetails): ticket is StaffTicketDetails {
  return 'requester' in ticket;
}

/** A status change in the staff timeline, with its reason. */
export interface StaffTimelineStatus extends RequesterTimelineStatus {
  reason: string | null;
}

/** A comment or an internal note (isInternal – yellow, "פנימי"; never sent to the requester). */
export interface StaffTimelineComment extends RequesterTimelineComment {
  isInternal: boolean;
}

export type TicketChangeKind = 'Reclassified' | 'Transferred' | 'Taken';

/** A reclassification or a transfer, with its reason (staff only). Names; null handler = the department's queue. */
export interface StaffTimelineChange {
  kind: 'change';
  at: string;
  changeKind: TicketChangeKind;
  changedByName: string;
  reason: string | null;
  fromCategoryName: string | null;
  toCategoryName: string | null;
  fromUrgency: Urgency | null;
  toUrgency: Urgency | null;
  fromDepartmentName: string | null;
  toDepartmentName: string | null;
  fromHandlerName: string | null;
  toHandlerName: string | null;
}

export type StaffTimelineItem =
  | StaffTimelineStatus
  | StaffTimelineComment
  | RequesterTimelineAttachment
  | StaffTimelineChange;

/** GET /api/tickets/{id}/history for staff: internal notes, reasons and changes included. */
export interface StaffTicketHistory {
  ticketId: number;
  items: StaffTimelineItem[];
}

export type TimelineItem = RequesterTimelineItem | StaffTimelineItem;

/** PATCH /classification: the fields not sent keep their value. */
export interface ReclassifyTicketRequest {
  categoryId?: number;
  urgency?: Urgency;
  reason: string;
  rowVersion: string;
}

/** POST /assign: exactly one target. No reason when a handler takes a ticket from the queue. */
export interface AssignTicketRequest {
  targetUserId?: number;
  targetDepartmentId?: number;
  reason?: string;
  rowVersion: string;
}

/** Response of a change. canView false: the ticket left the user's scope – back to the list. */
export interface TicketUpdated {
  id: number;
  rowVersion: string;
  canView: boolean;
}

/** GET /api/tickets/{id}/assign-options. */
export interface TicketAssignOptions {
  handlers: { id: number; name: string; openTicketCount: number }[];
  departments: NamedRef[];
}

/** Same limits as the server's TicketFieldLimits.Reason*. */
export const REASON_LIMITS = { minLength: 3, maxLength: 500 } as const;

// ===================== Bulk update (task 3.6, spec 11.2) =====================

/** Same limit as the server's BulkLimits.MaxItems. */
export const BULK_LIMITS = { maxItems: 100 } as const;

/** One selected ticket with the version of its row in the list. */
export interface BulkTicketItem {
  id: number;
  rowVersion: string;
}

/** POST /api/tickets/bulk/status – "התחל טיפול" only (a status that needs a message to the employee → 400). */
export interface BulkChangeStatusRequest {
  newStatus: 'InProgress';
  items: BulkTicketItem[];
}

/** POST /api/tickets/bulk/assign – one target and one required reason for every ticket. */
export interface BulkAssignRequest {
  targetUserId?: number;
  targetDepartmentId?: number;
  reason: string;
  items: BulkTicketItem[];
}

export type BulkFailureReason = 'Conflict' | 'NotFound' | 'InvalidTransition' | 'InvalidTarget';

/** errorType: the single action's error type (e.g. "ticket-assigned-to-other"); null for NotFound. */
export interface BulkFailure {
  id: number;
  reason: BulkFailureReason;
  errorType: string | null;
}

/** 200 even when some or all failed: the updated ids and the others with their reason, in the order sent. */
export interface BulkUpdateResult {
  succeeded: number[];
  failed: BulkFailure[];
}

/** The bulk actions of the queue. */
export type BulkAction = 'start' | 'assign';

/** GET /api/tickets/bulk/assign-options: handlers of the user's department (not the user), departments with handlers. */
export type BulkAssignOptions = TicketAssignOptions;
