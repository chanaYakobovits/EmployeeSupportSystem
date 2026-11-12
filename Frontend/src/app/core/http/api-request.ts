import { HttpRequest } from '@angular/common/http';

import { environment } from '../../../environments/environment';
import { AUTH_REQUEST, RETRIED_AFTER_REFRESH } from './http-context';

/** Only requests to our own API get the access token. */
export function isApiRequest(url: string): boolean {
  const base = environment.apiBaseUrl;
  return url === base || url.startsWith(`${base}/`);
}

/**
 * login / refresh / logout / set-password: authenticated by credentials, the refresh cookie or an invitation token –
 * never by an access token, and a failure is never "refresh and retry".
 */
const SESSION_ENDPOINTS = /\/auth\/(login|refresh|logout|set-password)$/;

export function isSessionRequest(request: HttpRequest<unknown>): boolean {
  return request.context.get(AUTH_REQUEST) || SESSION_ENDPOINTS.test(request.url.split('?')[0]);
}

/**
 * True when a 401 for this request is handled by auth.interceptor (refresh + one retry),
 * so error.interceptor must stay silent about it.
 */
export function refreshesOn401(request: HttpRequest<unknown>): boolean {
  return isApiRequest(request.url) && !isSessionRequest(request) && !request.context.get(RETRIED_AFTER_REFRESH);
}
