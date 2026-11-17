import { TicketStatus } from './ticket.models';

/**
 * The dashboards (task 5.1, Backend Common/DTOs/Dashboard). Every number is computed by the server; the definitions of
 * the metrics are the XML comments of DashboardCardsDto – the card explanations (dashboard-messages.ts) follow them.
 */

/** Which tickets a department dashboard counts (Backend DashboardView). */
export type DashboardView = 'Requesters' | 'Handling';

export const DASHBOARD_LIMITS = {
  /** The server's default range: 30 days, today included (DashboardLimits.DefaultRangeDays). */
  defaultRangeDays: 30,
  /** DashboardLimits.MaxRangeDays – a year, a leap year included. */
  maxRangeDays: 366,
} as const;

/** Calendar days in Israel (yyyy-MM-dd), both included. */
export interface DashboardRange {
  from: string;
  to: string;
}

export interface DashboardCards {
  /** Now: New, Assigned, InProgress, WaitingForEmployee. */
  openCount: number;
  /** Opened in the last 7 days, today included – not the range. */
  newThisWeekCount: number;
  /** Now: past the due time while the SLA clock runs. */
  overdueOpenCount: number;
  openedInRangeCount: number;
  resolvedInRangeCount: number;
  /** Of the tickets resolved in the range; null without any. */
  averageResolutionHours: number | null;
  slaCompliancePercent: number | null;
  /** Of the tickets opened in the range; null without any. */
  autoClassifiedPercent: number | null;
}

/** categoryId null = not classified yet. */
export interface DashboardCategoryCount {
  categoryId: number | null;
  categoryName: string | null;
  count: number;
}

/** Every status, in the enum's order, zeros included. */
export interface DashboardStatusCount {
  status: TicketStatus;
  count: number;
}

/** Every day of the range (yyyy-MM-dd, Israel). */
export interface DashboardTrendPoint {
  date: string;
  opened: number;
  closed: number;
}

/** "The oldest / overdue open tickets": overdue first, then the oldest. */
export interface DashboardTicket {
  id: number;
  subject: string;
  isSubjectExcerpt: boolean;
  requesterDepartmentName: string;
  assignedDepartmentName: string | null;
  status: TicketStatus;
  createdAt: string;
  dueAt: string | null;
  isOverdue: boolean;
}

/** What the department and the organization dashboards share. */
export interface DashboardMetrics {
  range: DashboardRange;
  generatedAt: string;
  cards: DashboardCards;
  byCategory: DashboardCategoryCount[];
  byStatus: DashboardStatusCount[];
  trend: DashboardTrendPoint[];
  oldestOpen: DashboardTicket[];
}

/** GET /api/dashboard/departments/{id}. */
export interface DepartmentDashboard extends DashboardMetrics {
  department: { id: number; name: string; isActive: boolean };
  view: DashboardView;
}

/** A row of the comparison, by the handling department. departmentId null = not routed yet. */
export interface DashboardDepartmentComparison {
  departmentId: number | null;
  departmentName: string | null;
  isActive: boolean;
  openCount: number;
  overdueOpenCount: number;
  resolvedInRangeCount: number;
  averageResolutionHours: number | null;
}

/** GET /api/dashboard/organization. */
export interface OrganizationDashboard extends DashboardMetrics {
  departments: DashboardDepartmentComparison[];
}

/** The range part of both queries. Both missing → the server's default (the last 30 days). */
export interface DashboardRangeQuery {
  from?: string | null;
  to?: string | null;
}

export interface DepartmentDashboardQuery extends DashboardRangeQuery {
  view?: DashboardView | null;
}
