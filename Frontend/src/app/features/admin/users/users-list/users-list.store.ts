import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

import { DepartmentsApiService } from '../../../../core/api/departments-api.service';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { Role } from '../../../../core/models/auth.models';
import { Department } from '../../../../core/models/department.models';
import {
  INVITATION_STATUS_VALUES,
  InvitationStatus,
  USER_SORT_FIELDS,
  UserListItem,
} from '../../../../core/models/user.models';
import { ListStateDef, boolParam, enumParam, idParam, textParam, toPageQuery } from '../../../../shared/list/list-state';
import { ListStore } from '../../../../shared/list/list-store';

export type UserFilters = {
  search: string | null;
  departmentId: number | null;
  role: Role | null;
  isActive: boolean | null;
  invitationStatus: InvitationStatus | null;
};

/** Every user is an Employee (spec 2.3), so filtering by it means nothing. */
export const FILTERABLE_ROLES: readonly Role[] = ['Handler', 'DepartmentManager', 'Executive', 'Admin'];

/** GET /api/users ↔ the query string of /admin/users. */
export const USERS_LIST: ListStateDef<UserFilters> = {
  filters: {
    search: textParam,
    departmentId: idParam,
    role: enumParam(FILTERABLE_ROLES),
    isActive: boolParam,
    invitationStatus: enumParam(INVITATION_STATUS_VALUES),
  },
  sortFields: USER_SORT_FIELDS,
  // Newest employees first. Same default as the server (UserSortFields.Default) – not written to the URL.
  defaultSort: { by: 'employeeCode', dir: 'desc' },
};

/** State of the users list screen (spec 7.8). Provided by UsersList. */
@Injectable()
export class UsersListStore {
  private readonly usersApi = inject(UsersApiService);
  private readonly departmentsApi = inject(DepartmentsApiService);

  readonly list = new ListStore<UserFilters, UserListItem>(USERS_LIST, (state) =>
    this.usersApi.list(toPageQuery(state)),
  );

  /** Options of the department filter (inactive ones too: their former employees can be looked up). */
  readonly departments = toSignal(this.departmentsApi.list(true).pipe(catchError(() => of<Department[]>([]))), {
    initialValue: [] as Department[],
  });
}
