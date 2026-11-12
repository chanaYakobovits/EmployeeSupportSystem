import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AUTH_REQUEST, skipErrorToast } from '../http/http-context';
import {
  AuthResponse,
  ChangePasswordRequest,
  CurrentUser,
  LoginRequest,
  SetPasswordRequest,
} from '../models/auth.models';

/** Typed calls to the backend AuthController (spec 9.1). No state – see AuthService. */
@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  /** Errors are shown by the login screen next to the form. */
  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, request, {
      withCredentials: true,
      context: sessionContext(),
    });
  }

  /** A failed refresh just means "not signed in" – never a toast. */
  refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/refresh`, null, {
      withCredentials: true,
      context: sessionContext(),
    });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, null, {
      withCredentials: true,
      context: sessionContext(),
    });
  }

  /** Errors are shown by the change-password screen next to the fields. */
  changePassword(request: ChangePasswordRequest): Observable<AuthResponse> {
    // Authenticated by the access token (not a session request), so an expired token is refreshed and retried.
    return this.http.post<AuthResponse>(`${this.baseUrl}/change-password`, request, {
      withCredentials: true,
      context: skipErrorToast(),
    });
  }

  /**
   * Invitation link → first password. Anonymous (no access token, no refresh on failure); errors are shown by the
   * set-password screen. 204 on success – no session is started.
   */
  setPassword(request: SetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/set-password`, request, {
      context: skipErrorToast(new HttpContext().set(AUTH_REQUEST, true)),
    });
  }

  me(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>(`${this.baseUrl}/me`);
  }
}

/** Session requests (login / refresh / logout): cookie credentials, no token refresh, errors handled by the caller. */
function sessionContext(): HttpContext {
  return skipErrorToast(new HttpContext().set(AUTH_REQUEST, true));
}
