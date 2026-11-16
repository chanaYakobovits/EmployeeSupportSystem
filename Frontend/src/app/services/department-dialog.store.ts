import { Injectable, Signal, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, debounceTime, distinctUntilChanged, finalize, map, of, switchMap, tap } from 'rxjs';

import { DepartmentsApiService } from './departments-api.service';
import { UsersApiService } from './users-api.service';
import { CreateDepartmentRequest, Department, UpdateDepartmentRequest } from '../models/department.models';
import { UserSummary } from '../models/user.models';
import { SEARCH_DEBOUNCE_MS } from '../components/shared/search-field/search-field';

/** Server search of users for an autocomplete field: the candidates for the typed text. */
export interface UserSearch {
  readonly candidates: Signal<UserSummary[]>;
  readonly searching: Signal<boolean>;
  search(term: string): void;
}

/**
 * State of the department dialog: manager candidates (active employees), departmental HR candidates (active handlers,
 * of any department – task 3.3) and saving. Provided by DepartmentDialog.
 */
@Injectable()
export class DepartmentDialogStore {
  private readonly usersApi = inject(UsersApiService);
  private readonly departmentsApi = inject(DepartmentsApiService);

  /** A manager is chosen among the active employees. */
  readonly managers = this.userSearch((term) => this.usersApi.lookupActive(term));

  /** The departmental HR is chosen among the active handlers. */
  readonly hrUsers = this.userSearch((term) => this.usersApi.lookupActive(term, 'Handler'));

  readonly saving = signal(false);

  create(request: CreateDepartmentRequest): Observable<Department> {
    this.saving.set(true);
    return this.departmentsApi.create(request).pipe(finalize(() => this.saving.set(false)));
  }

  update(id: number, request: UpdateDepartmentRequest): Observable<Department> {
    this.saving.set(true);
    return this.departmentsApi.update(id, request).pipe(finalize(() => this.saving.set(false)));
  }

  /** Same pattern as list search (spec 10.4): wait for a pause, skip repeats, cancel the previous request. */
  private userSearch(lookup: (term: string) => Observable<UserSummary[]>): UserSearch {
    const terms = new Subject<string>();
    const candidates = signal<UserSummary[]>([]);
    const searching = signal(false);

    terms
      .pipe(
        map((term) => term.trim()),
        debounceTime(SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        tap(() => searching.set(true)),
        switchMap((term) => lookup(term).pipe(catchError(() => of<UserSummary[]>([])))),
        takeUntilDestroyed(),
      )
      .subscribe((found) => {
        candidates.set(found);
        searching.set(false);
      });

    return {
      candidates: candidates.asReadonly(),
      searching: searching.asReadonly(),
      search: (term) => terms.next(term),
    };
  }
}
