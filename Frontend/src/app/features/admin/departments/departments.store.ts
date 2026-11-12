import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, finalize, tap } from 'rxjs';

import { DepartmentsApiService } from '../../../core/api/departments-api.service';
import { Department } from '../../../core/models/department.models';
import { LoadStatus } from '../../../shared/list/list-store';

/** State of the departments screen (spec 7.8). Provided by DepartmentsPage. */
@Injectable()
export class DepartmentsStore {
  private readonly api = inject(DepartmentsApiService);
  private readonly destroyRef = inject(DestroyRef);
  private loading?: Subscription;

  private readonly departmentsSignal = signal<Department[]>([]);
  private readonly statusSignal = signal<LoadStatus>('loading');

  /** Active and inactive, by name (the server's order). */
  readonly departments = this.departmentsSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  readonly isEmpty = computed(() => this.status() === 'ready' && this.departments().length === 0);
  /** The department whose active state is being changed. */
  readonly changingId = signal<number | null>(null);

  load(): void {
    this.loading?.unsubscribe();
    this.statusSignal.set('loading');
    this.loading = this.api
      .list(true)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (departments) => {
          this.departmentsSignal.set(departments);
          this.statusSignal.set('ready');
        },
        error: () => this.statusSignal.set('error'),
      });
  }

  /** After the dialog created or updated a department. */
  upsert(department: Department): void {
    const list = this.departmentsSignal();
    const exists = list.some((item) => item.id === department.id);
    const next = exists ? list.map((item) => (item.id === department.id ? department : item)) : [...list, department];
    this.departmentsSignal.set(next.sort((a, b) => a.name.localeCompare(b.name, 'he')));
  }

  setActive(department: Department, isActive: boolean): Observable<Department> {
    this.changingId.set(department.id);
    return this.api
      .update(department.id, { name: department.name, managerUserId: department.manager?.id ?? null, isActive })
      .pipe(
        tap((updated) => this.upsert(updated)),
        finalize(() => this.changingId.set(null)),
      );
  }
}
