import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { FormAlert, loginAlert } from '../auth-errors';
import { safeReturnUrl } from '../safe-return-url';

interface Feature {
  icon: string;
  text: string;
}

/** Spec 7.1 – login with e-mail or employee code. No sign-up, no "remember me", no "forgot password". */
@Component({
  selector: 'app-login-page',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  /** Bound from the query string (withComponentInputBinding). */
  readonly returnUrl = input<string>();

  protected readonly features: readonly Feature[] = [
    { icon: 'alt_route', text: 'ניתוב אוטומטי של כל פנייה לגורם המטפל המתאים' },
    { icon: 'timer', text: 'מעקב אחר זמני טיפול ויעדי שירות' },
    { icon: 'track_changes', text: 'עדכון שוטף על מצב הפנייה עד לפתרון' },
    { icon: 'monitoring', text: 'תמונת מצב ארגונית למנהלים' },
  ];

  protected readonly form = inject(NonNullableFormBuilder).group({
    identifier: ['', [Validators.required, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
  });

  protected readonly submitting = signal(false);
  protected readonly alert = signal<FormAlert | null>(null);
  protected readonly showPassword = signal(false);

  private readonly alertBox = viewChild<ElementRef<HTMLElement>>('alertBox');
  private readonly identifierInput = viewChild.required<ElementRef<HTMLInputElement>>('identifierInput');
  private readonly passwordInput = viewChild.required<ElementRef<HTMLInputElement>>('passwordInput');

  protected togglePassword(): void {
    this.showPassword.update((shown) => !shown);
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

    const { identifier, password } = this.form.getRawValue();
    this.submitting.set(true);

    this.auth
      .login({ identifier: identifier.trim(), password })
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (user) => {
          const target = user.mustChangePassword ? '/change-password' : safeReturnUrl(this.returnUrl());
          void this.router.navigateByUrl(target);
        },
        error: (error: unknown) => {
          this.alert.set(loginAlert(error));
          this.form.controls.password.reset();
          afterNextRender(() => this.alertBox()?.nativeElement.focus(), { injector: this.injector });
        },
      });
  }

  private focusFirstInvalid(): void {
    const target = this.form.controls.identifier.invalid ? this.identifierInput() : this.passwordInput();
    target.nativeElement.focus();
  }
}
