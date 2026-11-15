import { HttpParams } from '@angular/common/http';

export type QueryScalar = string | number | boolean | null | undefined;
/** An array is sent as a repeated parameter: ?status=New&status=Assigned. */
export type QueryValue = QueryScalar | readonly QueryScalar[];

/** Query string of a GET request. Empty values (null, undefined, '', []) are left out – the server applies its default. */
export function toHttpParams(values: Readonly<Record<string, QueryValue>>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    const items: readonly QueryScalar[] = Array.isArray(value) ? value : [value as QueryScalar];
    for (const item of items) {
      if (item !== null && item !== undefined && item !== '') {
        params = params.append(key, String(item));
      }
    }
  }
  return params;
}
