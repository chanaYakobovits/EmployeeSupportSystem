import { Role } from './auth.models';
import { DepartmentSummary } from './department.models';
import { PageQuery } from './paging.models';

/** Short reference to a user, e.g. a department manager (Backend UserSummaryDto). */
export interface UserSummary {
  id: number;
  employeeCode: string;
  firstName: string;
  lastName: string;
}

/** Row of GET /api/users (Backend UserListItemDto). */
export interface UserListItem {
  id: number;
  employeeCode: string;
  firstName: string;
  lastName: string;
  email: string;
  jobTitle: string;
  department: DepartmentSummary;
  roles: Role[];
  isActive: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

/** GET / PUT /api/users/{id} (Backend UserDetailsDto). */
export interface UserDetails extends UserListItem {
  mustChangePassword: boolean;
  isLockedOut: boolean;
  /** The department this user manages (at most one), or null. */
  managedDepartment: DepartmentSummary | null;
}

/** Closed list of sortBy values of GET /api/users (Backend UserSortFields). Default: employeeCode, descending. */
export const USER_SORT_FIELDS = ['name', 'employeeCode', 'email', 'department', 'createdAt', 'lastLoginAt'] as const;
export type UserSortField = (typeof USER_SORT_FIELDS)[number];

/** GET /api/users. All filters are optional and combined with AND. */
export interface UserListQuery extends PageQuery {
  /** Matches first name, last name, full name, e-mail or employee code. */
  search?: string | null;
  departmentId?: number | null;
  role?: Role | null;
  isActive?: boolean | null;
}

/**
 * POST /api/users (spec 4.2). The employee code is generated; Employee is always added by the server.
 * DepartmentManager cannot be chosen – it follows from the departments screen.
 */
export interface CreateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  departmentId: number;
  jobTitle: string;
  roles: Role[];
}

/** PUT /api/users/{id}. The employee code never changes. */
export type UpdateUserRequest = CreateUserRequest;

/** The temporary password appears only in this response – show it once, never keep it. */
export interface CreateUserResponse {
  user: UserDetails;
  temporaryPassword: string;
}

export interface ResetPasswordResponse {
  temporaryPassword: string;
}

/** unassignedTicketCount: unresolved tickets of a deactivated handler returned to their department queue. */
export interface SetUserActiveResponse {
  user: UserDetails;
  unassignedTicketCount: number;
}

export function fullName(user: Pick<UserSummary, 'firstName' | 'lastName'>): string {
  return `${user.firstName} ${user.lastName}`.trim();
}
