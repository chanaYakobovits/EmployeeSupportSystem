import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { newPasswordControls } from '../../../core/auth/password-policy';
import { problemMessages, problemType, isProblemDetails } from '../../../core/models/problem-details';
import { NotificationService } from '../../../core/notifications/notification.service';
import { NewPasswordFields } from '../../../shared/components/new-password-fields/new-password-fields';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { FormAlert, changePasswordAlert } from '../auth-errors';
import { safeReturnUrl } from '../safe-return-url';

type FieldName = 'currentPassword' | 'newPassword' | 'confirmPassword';

/** required: first login (MustChangePassword), outside the shell. voluntary: from the user menu, inside the shell. */
export type ChangePasswordMode = 'required' | 'voluntary';

/** The new password must differ from the current one (same rule as the server). */
const differentFromCurrent: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const current = control.parent?.get('currentPassword')?.value as string | undefined;
  return control.value && current && control.value === current ? { sameAsCurrent: true } : null;
};

/**
 * Spec 7.2 – password change, in two modes (route data.mode):
 * - required (first login): mandatory, no skip, no menu – outside the shell. Success → home.
 * - voluntary (user menu): inside the shell, with cancel. Success / cancel → back to returnUrl.
 * The policy (spec 6.1) is shown up front and checked as the user types; the server re-checks it.
 */
@Component({
  selector: 'app-change-password-page',
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    NewPasswordFields,
    PageHeader,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './change-password-page.html',
  styleUrl: './change-password-page.scss',
})
export class ChangePasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  /** From route data (withComponentInputBinding). */
  readonly mode = input<ChangePasswordMode>('required');
  /** From the query string: the screen the user came from (voluntary mode). */
  readonly returnUrl = input<string>();

  protected readonly isVoluntary = computed(() => this.mode() === 'voluntary');

  protected readonly form = inject(NonNullableFormBuilder).group({
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    ...newPasswordControls([differentFromCurrent]),
  });

  protected readonly submitting = signal(false);
  protected readonly alert = signal<FormAlert | null>(null);
  protected readonly showCurrent = signal(false);

  protected readonly firstName = computed(() => this.auth.currentUser()?.firstName ?? '');
  protected readonly username = computed(() => this.auth.currentUser()?.email ?? '');

  private readonly alertBox = viewChild<ElementRef<HTMLElement>>('alertBox');
  private readonly currentInput = viewChild.required<ElementRef<HTMLInputElement>>('currentInput');
  private readonly newPasswordFields = viewChild.required(NewPasswordFields);

  constructor() {
    // Cross-field rule: "different from the current one" depends on the current password.
    // (The confirmation's dependency on the new password is handled by NewPasswordFields.)
    const { currentPassword, newPassword } = this.form.controls;
    currentPassword.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => newPassword.dirty && newPassword.updateValueAndValidity({ emitEvent: false }));
  }

  protected toggleCurrentVisible(): void {
    this.showCurrent.update((shown) => !shown);
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }
    this.alert.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusFirstInvalid();
      return;
    }

    const { currentPassword, newPassword } = this.form.getRawValue();
    this.submitting.set(true);

    this.auth
      .changePassword({ currentPassword, newPassword })
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          this.notifications.success('הסיסמה הוחלפה בהצלחה');
          void this.router.navigateByUrl(this.isVoluntary() ? safeReturnUrl(this.returnUrl()) : '/');
        },
        error: (error: unknown) => this.showServerError(error),
      });
  }

  /** Voluntary mode only: leave without changing anything. */
  protected cancel(): void {
    void this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
  }

  /** Required mode: not a way to skip the change – a way out for someone who cannot complete it now. */
  protected logout(): void {
    void this.auth.logout();
  }

  private showServerError(error: unknown): void {
    const type = error instanceof HttpErrorResponse ? problemType(error) : null;
    const title = error instanceof HttpErrorResponse && isProblemDetails(error.error) ? error.error.title : undefined;

    if (type === 'wrong-current-password') {
      this.setServerError('currentPassword', title ?? 'הסיסמה הנוכחית שגויה');
      return;
    }
    if (type === 'password-policy' && error instanceof HttpErrorResponse) {
      const messages = problemMessages(error);
      this.setServerError('newPassword', messages.length ? messages.join(' · ') : (title ?? ''));
      return;
    }

    this.alert.set(changePasswordAlert(error));
    afterNextRender(() => this.alertBox()?.nativeElement.focus(), { injector: this.injector });
  }

  private setServerError(field: FieldName, message: string): void {
    const control = this.form.controls[field];
    control.setErrors({ server: message });
    control.markAsTouched();
    afterNextRender(() => this.focus(field), { injector: this.injector });
  }

  private focusFirstInvalid(): void {
    if (this.form.controls.currentPassword.invalid) {
      this.currentInput().nativeElement.focus();
      return;
    }
    this.newPasswordFields().focusFirstInvalid();
  }

  private focus(field: FieldName): void {
    if (field === 'currentPassword') {
      this.currentInput().nativeElement.focus();
    } else {
      this.newPasswordFields().focus(field);
    }
  }
}
