import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { TokenStore } from '../auth/token.store';
import { isApiRequest, isSessionRequest, refreshesOn401 } from '../http/api-request';
import { RETRIED_AFTER_REFRESH } from '../http/http-context';

/**
 * Adds the Bearer token to API requests. On 401 renews the token once (one refresh shared by all waiting
 * requests) and retries the request once. If the refresh fails: local sign-out and the login screen with returnUrl.
 * Registered before errorInterceptor.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApiRequest(req.url)) {
    return next(req); // never leak the token to other hosts
  }

  const auth = inject(AuthService);
  const tokens = inject(TokenStore);
  const router = inject(Router);

  return next(prepare(req, tokens.token())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !refreshesOn401(req)) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError(() => {
          auth.endSessionAndRedirect(router.url);
          return throwError(() => error);
        }),
        switchMap((token) => next(prepare(req, token, true))),
      );
    }),
  );
};

function prepare(req: HttpRequest<unknown>, token: string | null, retry = false): HttpRequest<unknown> {
  const session = isSessionRequest(req);
  return req.clone({
    // Session endpoints are authenticated by the refresh cookie (or credentials), not by the access token.
    setHeaders: token && !session ? { Authorization: `Bearer ${token}` } : {},
    withCredentials: session || req.withCredentials,
    context: retry ? req.context.set(RETRIED_AFTER_REFRESH, true) : req.context,
  });
}
