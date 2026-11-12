import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, finalize, tap } from 'rxjs';

import { SlaPoliciesApiService } from '../../../core/api/sla-policies-api.service';
import { SlaPolicies } from '../../../core/models/sla.models';
import { LoadStatus } from '../../../shared/list/list-store';

/** State of the SLA screen (spec 7.8). Provided by SlaPage. */
@Injectable()
export class SlaStore {
  private readonly api = inject(SlaPoliciesApiService);
  private readonly destroyRef = inject(DestroyRef);
  private loading?: Subscription;

  private readonly policiesSignal = signal<SlaPolicies | null>(null);
  private readonly statusSignal = signal<LoadStatus>('loading');

  /** As last loaded or saved. */
  readonly policies = this.policiesSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  readonly saving = signal(false);

  load(): void {
    this.loading?.unsubscribe();
    this.statusSignal.set('loading');
    this.loading = this.api
      .get()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (policies) => {
          this.policiesSignal.set(policies);
          this.statusSignal.set('ready');
        },
        error: () => this.statusSignal.set('error'),
      });
  }

  save(policies: SlaPolicies): Observable<SlaPolicies> {
    this.saving.set(true);
    return this.api.update(policies).pipe(
      tap((saved) => this.policiesSignal.set(saved)),
      finalize(() => this.saving.set(false)),
    );
  }
}
