import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, WritableSignal, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, defer, finalize, forkJoin, of, tap } from 'rxjs';

import { DepartmentsApiService } from '../../../../core/api/departments-api.service';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { Department } from '../../../../core/models/department.models';
import {
  CreateUserRequest,
  ResetPasswordResponse,
  SetUserActiveResponse,
  UpdateUserRequest,
  UserDetails,
} from '../../../../core/models/user.models';

export type UserFormStatus = 'loading' | 'ready' | 'error' | 'notFound';

/**
 * State of the user form (create / edit, spec 4.2, 7.8). Provided by UserForm.
 * Temporary passwords pass through the returned observables only – never stored here.
 */
@Injectable()
export class UserFormStore {
  private readonly usersApi = inject(UsersApiService);
  private readonly departmentsApi = inject(DepartmentsApiService);
  private readonly destroyRef = inject(DestroyRef);
  private loading?: Subscription;

  private readonly userSignal = signal<UserDetails | null>(null);
  private readonly editableSignal = signal<UserDetails | null>(null);
  private readonly departmentsSignal = signal<Department[]>([]);
  private readonly statusSignal = signal<UserFormStatus>('loading');

  /** The user as last returned by the server (edit mode), or null (create mode). */
  readonly user = this.userSignal.asReadonly();
  /**
   * The values the form should show: changes on load and save only. Deactivating does not reset the form,
   * so unsaved edits are not lost.
   */
  readonly editable = this.editableSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  readonly saving = signal(false);
  readonly resetting = signal(false);
  readonly changingActive = signal(false);
  readonly resendingInvitation = signal(false);
  /** Any action of the side card is running: the others wait. */
  readonly busy = computed(() => this.resetting() || this.changingActive() || this.resendingInvitation());

  /** Active departments, plus the user's current one even if it was deactivated since. */
  readonly departmentOptions = computed(() => {
    const currentId = this.user()?.department.id;
    return this.departmentsSignal().filter((department) => department.isActive || department.id === currentId);
  });

  /** id null → create mode. */
  load(id: number | null): void {
    this.loading?.unsubscribe();
    this.statusSignal.set('loading');
    this.loading = forkJoin({
      departments: this.departmentsApi.list(true),
      user: id === null ? of(null) : this.usersApi.get(id),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ departments, user }) => {
          this.departmentsSignal.set(departments);
          this.setUser(user, true);
          this.statusSignal.set('ready');
        },
        error: (error: unknown) => {
          this.statusSignal.set(error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error');
        },
      });
  }

  /** The route parameter is not a valid id. */
  markNotFound(): void {
    this.loading?.unsubscribe();
    this.statusSignal.set('notFound');
  }

  /** No password: the server queues an invitation e-mail. */
  create(request: CreateUserRequest): Observable<UserDetails> {
    return withFlag(this.saving, this.usersApi.create(request));
  }

  update(id: number, request: UpdateUserRequest): Observable<UserDetails> {
    return withFlag(this.saving, this.usersApi.update(id, request)).pipe(tap((user) => this.setUser(user, true)));
  }

  resetPassword(id: number): Observable<ResetPasswordResponse> {
    return withFlag(this.resetting, this.usersApi.resetPassword(id)).pipe(
      tap(() => {
        const user = this.userSignal();
        if (user) {
          this.userSignal.set({ ...user, mustChangePassword: true, isLockedOut: false });
        }
        // The reset also revoked an open invitation: fetch the new invitation state.
        this.refreshUser(id);
      }),
    );
  }

  /** Sends the invitation e-mail again; the response is the user with the new (Pending) invitation state. */
  resendInvitation(id: number): Observable<UserDetails> {
    return withFlag(this.resendingInvitation, this.usersApi.resendInvitation(id)).pipe(
      tap((user) => this.setUser(user, false)),
    );
  }

  /** Reloads the shown user without touching the form (unsaved edits stay). Best effort – errors are ignored. */
  private refreshUser(id: number): void {
    this.usersApi
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (user) => {
          if (this.userSignal()?.id === id) {
            this.setUser(user, false);
          }
        },
        error: () => undefined,
      });
  }

  setActive(id: number, isActive: boolean): Observable<SetUserActiveResponse> {
    return withFlag(this.changingActive, this.usersApi.setActive(id, isActive)).pipe(
      tap((response) => this.setUser(response.user, false)),
    );
  }

  private setUser(user: UserDetails | null, resetForm: boolean): void {
    this.userSignal.set(user);
    if (resetForm) {
      this.editableSignal.set(user);
    }
  }
}

/** flag is true while the request runs (subscribed and not finished). */
function withFlag<T>(flag: WritableSignal<boolean>, source: Observable<T>): Observable<T> {
  return defer(() => {
    flag.set(true);
    return source.pipe(finalize(() => flag.set(false)));
  });
}
