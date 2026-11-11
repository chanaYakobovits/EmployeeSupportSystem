import { Injectable, signal } from '@angular/core';

/**
 * Holds the access token in memory only (spec 6.1) – never localStorage / sessionStorage.
 * A page reload loses it on purpose; the session is restored from the HttpOnly refresh cookie.
 */
@Injectable({ providedIn: 'root' })
export class TokenStore {
  private readonly accessToken = signal<string | null>(null);

  readonly token = this.accessToken.asReadonly();

  set(token: string): void {
    this.accessToken.set(token);
  }

  clear(): void {
    this.accessToken.set(null);
  }
}
