import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { skipErrorToast } from './http-context';
import { toHttpParams } from './query-params';
import {
  DashboardRangeQuery,
  DepartmentDashboard,
  DepartmentDashboardQuery,
  OrganizationDashboard,
} from '../models/dashboard.models';

/**
 * Typed calls to the backend DashboardController (spec 9.3, task 5.1). No state.
 * The screens show the errors themselves (error state / alert), so no toast.
 */
@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/dashboard`;

  /** A department manager – their own department only (else 404); an executive – any department. */
  department(id: number, query: DepartmentDashboardQuery): Observable<DepartmentDashboard> {
    return this.http.get<DepartmentDashboard>(`${this.baseUrl}/departments/${id}`, {
      params: toHttpParams({ view: query.view ?? null, from: query.from ?? null, to: query.to ?? null }),
      context: skipErrorToast(),
    });
  }

  /** Executive only. */
  organization(query: DashboardRangeQuery): Observable<OrganizationDashboard> {
    return this.http.get<OrganizationDashboard>(`${this.baseUrl}/organization`, {
      params: toHttpParams({ from: query.from ?? null, to: query.to ?? null }),
      context: skipErrorToast(),
    });
  }
}
