import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, tap, throwError } from 'rxjs';

import { AuthApiService } from '../api/auth-api.service';
import { AuthResponse, ChangePasswordRequest, CurrentUser, LoginRequest, Role } from '../models/auth.models';
import { TokenStore } from './token.store';

/** Session state of the signed-in user, as signals. The only place that changes it. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApiService);
  private readonly tokens = inject(TokenStore);
  private readonly router = inject(Router);

  private readonly user = signal<CurrentUser | null>(null);

  /** The single refresh request in progress, shared by everyone who needs it (single-flight). */
  private refreshInFlight: Observable<string> | null = null;
  private redirectingToLogin = false;

  readonly currentUser = this.user.asReadonly();
  readonly isAuthenticated = computed(() => this.user() !== null && this.tokens.token() !== null);
  readonly roles = computed<readonly Role[]>(() => this.user()?.roles ?? []);
  readonly mustChangePassword = computed(() => this.user()?.mustChangePassword ?? false);

  hasAnyRole(roles: readonly Role[]): boolean {
    const mine = this.roles();
    return roles.some((role) => mine.includes(role));
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.api.login(request).pipe(map((response) => this.applySession(response)));
  }

  /** On success the server revokes all previous refresh tokens and returns a fresh session. */
  changePassword(request: ChangePasswordRequest): Observable<CurrentUser> {
    return this.api.changePassword(request).pipe(map((response) => this.applySession(response)));
  }

  /** Reloads the signed-in user's details (GET /auth/me). */
  me(): Observable<CurrentUser> {
    return this.api.me().pipe(tap((user) => this.user.set(user)));
  }

  /**
   * New access token from the refresh cookie. Concurrent callers share one request.
   * Fails (and clears the session) when the cookie is missing, expired or revoked.
   */
  refresh(): Observable<string> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.api.refresh().pipe(
        map((response) => {
          this.applySession(response);
          return response.accessToken;
        }),
        catchError((error: unknown) => {
          this.clearSession();
          return throwError(() => error);
        }),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }
    return this.refreshInFlight;
  }

  /** App start: one refresh attempt to restore the session after a reload. Never fails, never shows an error. */
  restoreSession(): Promise<void> {
    return firstValueFrom(
      this.refresh().pipe(
        map(() => undefined),
        catchError(() => of(undefined)),
      ),
    );
  }

  /** Revokes the refresh token on the server (best effort), clears the local session and opens the login screen. */
  async logout(): Promise<void> {
    await firstValueFrom(this.api.logout().pipe(catchError(() => of(undefined))), { defaultValue: undefined });
    this.clearSession();
    await this.router.navigateByUrl('/login');
  }

  /** Clears the session in this tab only (no server call). */
  logoutLocal(): void {
    this.clearSession();
  }

  /** The session could not be renewed: sign out locally and open the login screen, returning to returnUrl afterwards. */
  endSessionAndRedirect(returnUrl: string): void {
    this.clearSession();
    if (this.redirectingToLogin) {
      return; // several pending requests fail together – navigate once
    }
    this.redirectingToLogin = true;
    const keepReturnUrl = !!returnUrl && returnUrl !== '/' && !returnUrl.startsWith('/login');
    void this.router
      .navigate(['/login'], { queryParams: keepReturnUrl ? { returnUrl } : {} })
      .finally(() => (this.redirectingToLogin = false));
  }

  private applySession(response: AuthResponse): CurrentUser {
    this.tokens.set(response.accessToken);
    const user: CurrentUser = { ...response.user, mustChangePassword: response.mustChangePassword };
    this.user.set(user);
    return user;
  }

  private clearSession(): void {
    this.tokens.clear();
    this.user.set(null);
  }
}
