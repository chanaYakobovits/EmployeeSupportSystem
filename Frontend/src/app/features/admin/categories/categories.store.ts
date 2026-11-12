import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, finalize, tap } from 'rxjs';

import { CategoriesApiService } from '../../../core/api/categories-api.service';
import { Category, CategorySaveResult } from '../../../core/models/category.models';
import { LoadStatus } from '../../../shared/list/list-store';

/** Same order as the server: by name, the fallback category last. */
function byNameFallbackLast(a: Category, b: Category): number {
  return Number(a.isFallback) - Number(b.isFallback) || a.name.localeCompare(b.name, 'he');
}

/** State of the categories screen (spec 7.8). Provided by CategoriesPage. */
@Injectable()
export class CategoriesStore {
  private readonly api = inject(CategoriesApiService);
  private readonly destroyRef = inject(DestroyRef);
  private loading?: Subscription;

  private readonly categoriesSignal = signal<Category[]>([]);
  private readonly statusSignal = signal<LoadStatus>('loading');

  /** Active and inactive. */
  readonly categories = this.categoriesSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  readonly isEmpty = computed(() => this.status() === 'ready' && this.categories().length === 0);
  /** The category whose active state is being changed. */
  readonly changingId = signal<number | null>(null);

  load(): void {
    this.loading?.unsubscribe();
    this.statusSignal.set('loading');
    this.loading = this.api
      .list(true)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => {
          this.categoriesSignal.set(categories);
          this.statusSignal.set('ready');
        },
        error: () => this.statusSignal.set('error'),
      });
  }

  /** After the dialog created or updated a category. */
  upsert(category: Category): void {
    const list = this.categoriesSignal();
    const exists = list.some((item) => item.id === category.id);
    const next = exists ? list.map((item) => (item.id === category.id ? category : item)) : [...list, category];
    this.categoriesSignal.set(next.sort(byNameFallbackLast));
  }

  setActive(category: Category, isActive: boolean): Observable<CategorySaveResult> {
    this.changingId.set(category.id);
    return this.api
      .update(category.id, {
        name: category.name,
        description: category.description,
        routingType: category.routingType,
        handlingDepartmentId: category.handlingDepartment?.id ?? null,
        isActive,
      })
      .pipe(
        tap((result) => this.upsert(result.category)),
        finalize(() => this.changingId.set(null)),
      );
  }
}
