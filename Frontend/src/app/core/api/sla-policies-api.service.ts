import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { skipErrorToast } from '../http/http-context';
import { SlaPolicies } from '../models/sla.models';

/** Typed calls to the backend SlaPoliciesController (spec 9.3). Read: everyone; change: Admin. No state. */
@Injectable({ providedIn: 'root' })
export class SlaPoliciesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/sla-policies`;

  get(): Observable<SlaPolicies> {
    return this.http.get<SlaPolicies>(this.baseUrl, { context: skipErrorToast() });
  }

  /** All four levels together. Applies to new tickets only. */
  update(policies: SlaPolicies): Observable<SlaPolicies> {
    return this.http.put<SlaPolicies>(this.baseUrl, policies, { context: skipErrorToast() });
  }
}
