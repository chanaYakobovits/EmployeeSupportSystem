import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { skipErrorToast } from '../http/http-context';
import { toHttpParams } from '../http/query-params';
import { CreateDepartmentRequest, Department, UpdateDepartmentRequest } from '../models/department.models';

/**
 * Typed calls to the backend DepartmentsController (spec 9.3). No state.
 * The list is read by every signed-in user; includeInactive and the changes are Admin only.
 * The calling screen shows the errors itself, so no toast.
 */
@Injectable({ providedIn: 'root' })
export class DepartmentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/departments`;

  /** By name; not paged (an organization has a handful of departments). */
  list(includeInactive = false): Observable<Department[]> {
    return this.http.get<Department[]>(this.baseUrl, {
      params: toHttpParams({ includeInactive: includeInactive || null }),
      context: skipErrorToast(),
    });
  }

  create(request: CreateDepartmentRequest): Observable<Department> {
    return this.http.post<Department>(this.baseUrl, request, { context: skipErrorToast() });
  }

  update(id: number, request: UpdateDepartmentRequest): Observable<Department> {
    return this.http.put<Department>(`${this.baseUrl}/${id}`, request, { context: skipErrorToast() });
  }
}
