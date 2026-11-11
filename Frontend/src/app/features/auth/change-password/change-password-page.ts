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
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
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
import { PASSWORD_RULES, passwordPolicyValidator } from '../../../core/auth/password-policy';
import { problemMessages, problemType, isProblemDetails } from '../../../core/models/problem-details';
import { NotificationService } from '../../../core/notifications/notification.service';
import { PasswordStrengthMeter } from '../../../shared/components/password-strength/password-strength';
import { FormAlert, changePasswordAlert } from '../auth-errors';

type FieldName = 'currentPassword' | 'newPassword' | 'confirmPassword';
type RuleState = 'pending' | 'met' | 'unmet';

/** The new password must differ from the current one (same rule as the server). */
const differentFromCurrent: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const current = control.parent?.get('currentPassword')?.value as string | undefined;
  return control.value && current && control.value === current ? { sameAsCurrent: true } : null;
};

const matchesNewPassword: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const newPassword = control.parent?.get('newPassword')?.value as string | undefined;
  return control.value && control.value !== newPassword ? { mismatch: true } : null;
};

/**
 * Spec 7.2 – first-login password change. Mandatory: no skip and no menu (outside the shell).
 * The policy (spec 6.1) is shown up front and checked as the user types; the server re-checks it.
 */
@Component({
  selector: 'app-change-password-page',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    PasswordStrengthMeter,
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

  protected readonly form = inject(NonNullableFormBuilder).group({
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    newPassword: ['', [Validators.required, passwordPolicyValidator, differentFromCurrent]],
    confirmPassword: ['', [Validators.required, matchesNewPassword]],
  });

  protected readonly submitting = signal(false);
  protected readonly alert = signal<FormAlert | null>(null);
  protected readonly visible = signal<Record<FieldName, boolean>>({
    currentPassword: false,
    newPassword: false,
    confirmPassword: false,
  });

  protected readonly firstName = computed(() => this.auth.currentUser()?.firstName ?? '');
  protected readonly username = computed(() => this.auth.currentUser()?.email ?? '');

  private readonly newPasswordValue = toSignal(this.form.controls.newPassword.valueChanges, { initialValue: '' });
  protected readonly newPassword = computed(() => this.newPasswordValue() ?? '');

  protected readonly rules = computed(() => {
    const value = this.newPassword();
    return PASSWORD_RULES.map((rule) => ({
      id: rule.id,
      label: rule.label,
      state: (!value ? 'pending' : rule.test(value) ? 'met' : 'unmet') as RuleState,
    }));
  });

  private readonly alertBox = viewChild<ElementRef<HTMLElement>>('alertBox');
  private readonly currentInput = viewChild.required<ElementRef<HTMLInputElement>>('currentInput');
  private readonly newInput = viewChild.required<ElementRef<HTMLInputElement>>('newInput');
  private readonly confirmInput = viewChild.required<ElementRef<HTMLInputElement>>('confirmInput');

  constructor() {
    // Cross-field rules: re-validate the dependent field when the one it depends on changes.
    const { currentPassword, newPassword, confirmPassword } = this.form.controls;
    currentPassword.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => newPassword.dirty && newPassword.updateValueAndValidity({ emitEvent: false }));
    newPassword.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => confirmPassword.dirty && confirmPassword.updateValueAndValidity({ emitEvent: false }));
  }

  protected isVisible(field: FieldName): boolean {
    return this.visible()[field];
  }

  protected toggleVisible(field: FieldName): void {
    this.visible.update((state) => ({ ...state, [field]: !state[field] }));
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
          void this.router.navigateByUrl('/');
        },
        error: (error: unknown) => this.showServerError(error),
      });
  }

  /** Not a way to skip the change – a way out for someone who cannot complete it now. */
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
    afterNextRender(() => this.inputOf(field).nativeElement.focus(), { injector: this.injector });
  }

  private focusFirstInvalid(): void {
    const field = (['currentPassword', 'newPassword', 'confirmPassword'] as const).find(
      (name) => this.form.controls[name].invalid,
    );
    if (field) {
      this.inputOf(field).nativeElement.focus();
    }
  }

  private inputOf(field: FieldName): ElementRef<HTMLInputElement> {
    switch (field) {
      case 'currentPassword':
        return this.currentInput();
      case 'newPassword':
        return this.newInput();
      case 'confirmPassword':
        return this.confirmInput();
    }
  }
}
