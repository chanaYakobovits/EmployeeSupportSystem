import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { skipErrorToast } from '../http/http-context';
import { toHttpParams } from '../http/query-params';
import { PagedResult } from '../models/paging.models';
import {
  CreateUserRequest,
  ResetPasswordResponse,
  SetUserActiveResponse,
  UpdateUserRequest,
  UserDetails,
  UserListItem,
  UserListQuery,
  UserSummary,
} from '../models/user.models';

/**
 * Typed calls to the backend UsersController (spec 9.3), Admin only. No state.
 * Every screen that calls these shows the error itself (error state, field errors or an alert), so no toast.
 */
@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/users`;

  list(query: UserListQuery): Observable<PagedResult<UserListItem>> {
    return this.http.get<PagedResult<UserListItem>>(this.baseUrl, {
      params: toHttpParams({ ...query }),
      context: skipErrorToast(),
    });
  }

  /** Active users matching the search, e.g. to choose a department manager. */
  lookupActive(search: string, pageSize = 20): Observable<UserSummary[]> {
    return this.list({ search, isActive: true, page: 1, pageSize, sortBy: 'name', sortDir: 'asc' }).pipe(
      map((page) =>
        page.items.map(({ id, employeeCode, firstName, lastName }) => ({ id, employeeCode, firstName, lastName })),
      ),
    );
  }

  get(id: number): Observable<UserDetails> {
    return this.http.get<UserDetails>(`${this.baseUrl}/${id}`, { context: skipErrorToast() });
  }

  create(request: CreateUserRequest): Observable<UserDetails> {
    return this.http.post<UserDetails>(this.baseUrl, request, { context: skipErrorToast() });
  }

  update(id: number, request: UpdateUserRequest): Observable<UserDetails> {
    return this.http.put<UserDetails>(`${this.baseUrl}/${id}`, request, { context: skipErrorToast() });
  }

  resetPassword(id: number): Observable<ResetPasswordResponse> {
    return this.http.post<ResetPasswordResponse>(`${this.baseUrl}/${id}/reset-password`, null, {
      context: skipErrorToast(),
    });
  }

  /** Sends the invitation e-mail again (earlier links stop working). 422 if the user already chose a password. */
  resendInvitation(id: number): Observable<UserDetails> {
    return this.http.post<UserDetails>(`${this.baseUrl}/${id}/invitation`, null, { context: skipErrorToast() });
  }

  setActive(id: number, isActive: boolean): Observable<SetUserActiveResponse> {
    return this.http.patch<SetUserActiveResponse>(
      `${this.baseUrl}/${id}/active`,
      { isActive },
      { context: skipErrorToast() },
    );
  }
}
