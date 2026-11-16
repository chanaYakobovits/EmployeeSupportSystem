import { UserSummary } from './user.models';

/** Short reference to a department (Backend DepartmentSummaryDto). */
export interface DepartmentSummary {
  id: number;
  name: string;
}

/** GET /api/departments (Backend DepartmentDto). */
export interface Department {
  id: number;
  name: string;
  isActive: boolean;
  manager: UserSummary | null;
  /**
   * The departmental HR handler (task 3.3): receives the attendance / leave tickets of the department's employees.
   * Possibly of another department. null = those tickets go to central HR.
   */
  hrUser: UserSummary | null;
  activeUserCount: number;
  /** Active users of the department with the Handler role. */
  handlerCount: number;
}

/** POST /api/departments. A new department is always active. */
export interface CreateDepartmentRequest {
  name: string;
  managerUserId: number | null;
  hrUserId: number | null;
}

/** PUT /api/departments/{id}. managerUserId null = no manager; hrUserId null = no departmental HR. */
export interface UpdateDepartmentRequest {
  name: string;
  managerUserId: number | null;
  isActive: boolean;
  hrUserId: number | null;
}
