import { HttpRequest } from '@angular/common/http';

import { environment } from '../../../environments/environment';
import { AUTH_REQUEST, RETRIED_AFTER_REFRESH } from './http-context';

/** Only requests to our own API get the access token. */
export function isApiRequest(url: string): boolean {
  const base = environment.apiBaseUrl;
  return url === base || url.startsWith(`${base}/`);
}

/** login / refresh / logout: authenticated by credentials or the refresh cookie, never by a refreshed token. */
const SESSION_ENDPOINTS = /\/auth\/(login|refresh|logout)$/;

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
