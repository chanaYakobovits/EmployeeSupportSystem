import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 ProblemDetails as returned by the API (see Backend ExceptionHandlingMiddleware). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  correlationId?: string;
  /**
   * Model validation (ASP.NET): field name → messages.
   * Business validation (e.g. password policy): a flat list of user-facing messages.
   */
  errors?: Record<string, string[]> | string[];
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && 'title' in value && 'status' in value;
}

/** The last segment of the ProblemDetails "type" URI (e.g. "account-locked"), or null. */
export function problemType(error: HttpErrorResponse): string | null {
  if (!isProblemDetails(error.error) || !error.error.type) {
    return null;
  }
  const type = error.error.type;
  return type.substring(type.lastIndexOf('/') + 1) || null;
}

/** All error messages of a ProblemDetails, whichever "errors" shape it uses. */
export function problemMessages(error: HttpErrorResponse): string[] {
  if (!isProblemDetails(error.error) || !error.error.errors) {
    return [];
  }
  const errors = error.error.errors;
  return Array.isArray(errors) ? errors : Object.values(errors).flat();
}
