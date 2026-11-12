import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';

import { Role } from '../../../../core/models/auth.models';
import { UserListItem } from '../../../../core/models/user.models';
import { ActiveBadge } from '../../../../shared/components/active-badge/active-badge';
import { Badge } from '../../../../shared/components/badge/badge';
import { DataTable, DataTableCell, DataTableColumn } from '../../../../shared/components/data-table/data-table';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { FilterBar } from '../../../../shared/components/filter-bar/filter-bar';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { SearchField } from '../../../../shared/components/search-field/search-field';
import { ROLE_LABELS, RoleLabelPipe } from '../../../../shared/pipes/role-label.pipe';
import { USERS_URL } from '../../admin-errors';
import { FILTERABLE_ROLES, UsersListStore } from './users-list.store';

export type ActiveFilter = 'all' | 'active' | 'inactive';

/** Shown in the roles column: the most senior first; "Employee" only when it is the only role. */
const ROLE_ORDER: readonly Role[] = ['Admin', 'Executive', 'DepartmentManager', 'Handler'];

/** Users list (spec 7.8): search, filters, server-side sort and paging; the state lives in the URL. */
@Component({
  selector: 'app-users-list',
  imports: [
    RouterLink,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    PageHeader,
    FilterBar,
    SearchField,
    DataTable,
    DataTableCell,
    EmptyState,
    ActiveBadge,
    Badge,
    RoleLabelPipe,
  ],
  providers: [UsersListStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-list.html',
  styleUrl: './users-list.scss',
})
export class UsersList {
  private readonly store = inject(UsersListStore);
  private readonly router = inject(Router);

  protected readonly list = this.store.list;
  protected readonly state = this.list.state;
  protected readonly departments = this.store.departments;
  protected readonly roleOptions = FILTERABLE_ROLES.map((role) => ({ role, label: ROLE_LABELS[role] }));

  protected readonly columns: readonly DataTableColumn[] = [
    { key: 'employeeCode', header: 'קוד עובד', sortKey: 'employeeCode' },
    { key: 'name', header: 'שם', sortKey: 'name' },
    { key: 'email', header: 'מייל', sortKey: 'email' },
    { key: 'department', header: 'מחלקה', sortKey: 'department' },
    { key: 'jobTitle', header: 'תפקיד בארגון' },
    { key: 'roles', header: 'תפקידי מערכת' },
    { key: 'isActive', header: 'סטטוס' },
  ];

  protected readonly activeFilter = computed<ActiveFilter>(() => {
    const isActive = this.state().filters.isActive;
    return isActive === null ? 'all' : isActive ? 'active' : 'inactive';
  });

  /** Lets the user form go back to this exact view (search, filters, page). */
  protected readonly returnState = computed(() => ({
    returnTo: this.router.serializeUrl(this.router.createUrlTree([USERS_URL], { queryParams: this.list.queryParams() })),
  }));

  protected systemRoles(user: UserListItem): Role[] {
    const roles = ROLE_ORDER.filter((role) => user.roles.includes(role));
    return roles.length ? roles : ['Employee'];
  }

  protected onSearch(text: string): void {
    this.list.setFilters({ search: text || null }, { replaceUrl: true });
  }

  protected onDepartment(departmentId: number | null): void {
    this.list.setFilters({ departmentId });
  }

  protected onRole(role: Role | null): void {
    this.list.setFilters({ role });
  }

  protected onActive(value: ActiveFilter): void {
    this.list.setFilters({ isActive: value === 'all' ? null : value === 'active' });
  }

  protected openUser(user: UserListItem): void {
    void this.router.navigate([USERS_URL, user.id], { state: this.returnState() });
  }
}
