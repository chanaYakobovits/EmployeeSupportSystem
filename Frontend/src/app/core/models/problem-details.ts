/** RFC 7807 ProblemDetails as returned by the API (see Backend ExceptionHandlingMiddleware). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  correlationId?: string;
  /** Present on 400 validation errors: field name → messages. */
  errors?: Record<string, string[]>;
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && 'title' in value && 'status' in value;
}
