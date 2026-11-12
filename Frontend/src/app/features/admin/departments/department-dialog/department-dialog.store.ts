import { Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, debounceTime, distinctUntilChanged, finalize, map, of, switchMap, tap } from 'rxjs';

import { DepartmentsApiService } from '../../../../core/api/departments-api.service';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { CreateDepartmentRequest, Department, UpdateDepartmentRequest } from '../../../../core/models/department.models';
import { UserSummary } from '../../../../core/models/user.models';
import { SEARCH_DEBOUNCE_MS } from '../../../../shared/components/search-field/search-field';

/** State of the department dialog: manager candidates (server search) and saving. Provided by DepartmentDialog. */
@Injectable()
export class DepartmentDialogStore {
  private readonly usersApi = inject(UsersApiService);
  private readonly departmentsApi = inject(DepartmentsApiService);
  private readonly terms = new Subject<string>();

  /** Active users matching the typed text – a manager is chosen among active employees. */
  readonly candidates = signal<UserSummary[]>([]);
  readonly searching = signal(false);
  readonly saving = signal(false);

  constructor() {
    // Same pattern as list search (spec 10.4): wait for a pause, skip repeats, cancel the previous request.
    this.terms
      .pipe(
        map((term) => term.trim()),
        debounceTime(SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        tap(() => this.searching.set(true)),
        switchMap((term) => this.usersApi.lookupActive(term).pipe(catchError(() => of<UserSummary[]>([])))),
        takeUntilDestroyed(),
      )
      .subscribe((candidates) => {
        this.candidates.set(candidates);
        this.searching.set(false);
      });
  }

  search(term: string): void {
    this.terms.next(term);
  }

  create(request: CreateDepartmentRequest): Observable<Department> {
    this.saving.set(true);
    return this.departmentsApi.create(request).pipe(finalize(() => this.saving.set(false)));
  }

  update(id: number, request: UpdateDepartmentRequest): Observable<Department> {
    this.saving.set(true);
    return this.departmentsApi.update(id, request).pipe(finalize(() => this.saving.set(false)));
  }
}
