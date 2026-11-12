import { DepartmentSummary } from './department.models';

/** System roles (Backend Common.Enums.Role). A user may hold several; every user is also an Employee. */
export type Role = 'Employee' | 'Handler' | 'DepartmentManager' | 'Executive' | 'Admin';

/** CurrentUserDto – GET /api/auth/me and the "user" part of every AuthResponse. */
export interface CurrentUser {
  id: number;
  employeeCode: string;
  firstName: string;
  lastName: string;
  email: string;
  jobTitle: string;
  department: DepartmentSummary;
  roles: Role[];
  mustChangePassword: boolean;
}

/** Body of login / refresh / change-password. The refresh token travels only in the HttpOnly cookie. */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  mustChangePassword: boolean;
  user: CurrentUser;
}

/** One identifier field: contains '@' → e-mail, otherwise → employee code (spec 4.1). */
export interface LoginRequest {
  identifier: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

/** POST /auth/set-password (anonymous): the token from the invitation link + the first password. */
export interface SetPasswordRequest {
  token: string;
  newPassword: string;
}
