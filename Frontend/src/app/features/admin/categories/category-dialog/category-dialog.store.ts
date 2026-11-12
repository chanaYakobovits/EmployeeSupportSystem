import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, finalize } from 'rxjs';

import { CategoriesApiService } from '../../../../core/api/categories-api.service';
import { DepartmentsApiService } from '../../../../core/api/departments-api.service';
import {
  CategorySaveResult,
  CreateCategoryRequest,
  UpdateCategoryRequest,
} from '../../../../core/models/category.models';
import { DepartmentSummary } from '../../../../core/models/department.models';

/** State of the category dialog: the handling department options and saving. Provided by CategoryDialog. */
@Injectable()
export class CategoryDialogStore {
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly departmentsApi = inject(DepartmentsApiService);
  private readonly destroyRef = inject(DestroyRef);

  /** Active departments – a category can only be routed to an active one. */
  readonly departments = signal<DepartmentSummary[]>([]);
  readonly departmentsFailed = signal(false);
  readonly saving = signal(false);

  loadDepartments(): void {
    this.departmentsFailed.set(false);
    this.departmentsApi
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (departments) => this.departments.set(departments.map(({ id, name }) => ({ id, name }))),
        error: () => this.departmentsFailed.set(true),
      });
  }

  create(request: CreateCategoryRequest): Observable<CategorySaveResult> {
    this.saving.set(true);
    return this.categoriesApi.create(request).pipe(finalize(() => this.saving.set(false)));
  }

  update(id: number, request: UpdateCategoryRequest): Observable<CategorySaveResult> {
    this.saving.set(true);
    return this.categoriesApi.update(id, request).pipe(finalize(() => this.saving.set(false)));
  }
}
