import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, catchError, map, of } from 'rxjs';

import { AuthService } from './auth.service';
import { DepartmentsApiService } from './departments-api.service';
import { Role } from '../models/auth.models';
import { Department } from '../models/department.models';
import { ORGANIZATION_DASHBOARD_URL, departmentDashboardUrl } from '../components/dashboard/dashboard-urls';

export type DashboardLandingStatus = 'loading' | 'error' | 'noDepartment';

/**
 * Where /dashboard leads (spec 4.5): the executive → the organization dashboard; a department manager → the dashboard
 * of the department they manage (Departments.ManagerUserId – not necessarily their own department). Pure.
 */
export function dashboardLandingUrl(roles: readonly Role[], userId: number | null, departments: readonly Department[]): string | null {
  if (roles.includes('Executive')) {
    return ORGANIZATION_DASHBOARD_URL;
  }
  const managed = userId === null ? undefined : departments.find((d) => d.manager?.id === userId);
  return managed ? departmentDashboardUrl(managed.id) : null;
}

/** The landing of the dashboard area: finds the target (one GET /api/departments for a manager). */
@Injectable()
export class DashboardLandingStore {
  private readonly auth = inject(AuthService);
  private readonly departmentsApi = inject(DepartmentsApiService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly statusSignal = signal<DashboardLandingStatus>('loading');
  readonly status = this.statusSignal.asReadonly();

  /** Emits the URL to go to; nothing when there is none (status says why). */
  resolve(): Observable<string | null> {
    const roles = this.auth.roles();
    const userId = this.auth.currentUser()?.id ?? null;
    this.statusSignal.set('loading');

    const direct = dashboardLandingUrl(roles, userId, []);
    if (direct !== null) {
      return of(direct);
    }
    return this.departmentsApi.list().pipe(
      map((departments) => {
        const url = dashboardLandingUrl(roles, userId, departments);
        if (url === null) {
          this.statusSignal.set('noDepartment');
        }
        return url;
      }),
      catchError(() => {
        this.statusSignal.set('error');
        return of(null);
      }),
      takeUntilDestroyed(this.destroyRef),
    );
  }
}
