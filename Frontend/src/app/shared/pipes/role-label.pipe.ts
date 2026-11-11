import { Pipe, PipeTransform } from '@angular/core';

import { Role } from '../../core/models/auth.models';

/** Hebrew role names (spec 2.3). */
export const ROLE_LABELS: Readonly<Record<Role, string>> = {
  Employee: 'עובד',
  Handler: 'גורם מטפל',
  DepartmentManager: 'מנהל/ת מחלקה',
  Executive: 'מנכ"ל',
  Admin: 'מנהל/ת מערכת',
};

/** Most senior first – used where a single role is shown for a user with several. */
const SENIORITY: readonly Role[] = ['Admin', 'Executive', 'DepartmentManager', 'Handler', 'Employee'];

export function roleLabel(role: Role | null | undefined): string {
  return role ? (ROLE_LABELS[role] ?? role) : '';
}

/** The most senior of the user's roles (every user is at least an Employee). */
export function primaryRole(roles: readonly Role[]): Role {
  return SENIORITY.find((role) => roles.includes(role)) ?? 'Employee';
}

@Pipe({ name: 'roleLabel' })
export class RoleLabelPipe implements PipeTransform {
  transform(role: Role | null | undefined): string {
    return roleLabel(role);
  }
}
