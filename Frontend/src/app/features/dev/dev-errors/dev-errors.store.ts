import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';

import { DevErrorKind, DevErrorsApiService } from '../../../core/api/dev-errors-api.service';
import { ProblemDetails, isProblemDetails } from '../../../core/models/problem-details';

/** What the calling component received after the interceptor re-threw the error. */
export interface DevErrorResult {
  kind: DevErrorKind;
  status: number;
  problem: ProblemDetails | null;
}

/** TEMPORARY – state for the dev error test page (components only display). */
@Injectable()
export class DevErrorsStore {
  private readonly api = inject(DevErrorsApiService);

  readonly loading = signal<DevErrorKind | null>(null);
  readonly lastResult = signal<DevErrorResult | null>(null);

  trigger(kind: DevErrorKind): void {
    this.loading.set(kind);
    this.lastResult.set(null);

    this.api.trigger(kind).subscribe({
      next: () => {
        this.lastResult.set({ kind, status: 200, problem: null });
        this.loading.set(null);
      },
      error: (error: unknown) => {
        const httpError = error instanceof HttpErrorResponse ? error : null;
        this.lastResult.set({
          kind,
          status: httpError?.status ?? -1,
          problem: httpError && isProblemDetails(httpError.error) ? httpError.error : null,
        });
        this.loading.set(null);
      },
    });
  }
}
