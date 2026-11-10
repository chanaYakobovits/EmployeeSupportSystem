import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

/** Endpoints of the backend DevErrorsController (Development only). */
export type DevErrorKind = 'not-found' | 'conflict' | 'business-rule' | 'unexpected' | 'model-validation' | 'network';

/** TEMPORARY – calls the backend DevErrorsController to exercise the error pipeline. Delete with it. */
@Injectable({ providedIn: 'root' })
export class DevErrorsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/dev/errors`;

  trigger(kind: DevErrorKind): Observable<unknown> {
    switch (kind) {
      case 'model-validation':
        return this.http.post(`${this.baseUrl}/model-validation`, { name: 'a name that is far too long' });
      case 'network':
        // Port 9 (discard) is normally closed: simulates a server that is down.
        return this.http.get('http://localhost:9/api/unreachable');
      default:
        return this.http.get(`${this.baseUrl}/${kind}`);
    }
  }
}
