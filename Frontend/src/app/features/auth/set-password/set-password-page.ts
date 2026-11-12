import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, NavigationEnd, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { filter, finalize, take } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { newPasswordControls } from '../../../core/auth/password-policy';
import { isProblemDetails, problemMessages, problemType } from '../../../core/models/problem-details';
import { NewPasswordFields } from '../../../shared/components/new-password-fields/new-password-fields';
import { AuthBrand } from '../auth-brand/auth-brand';
import { FormAlert, changePasswordAlert } from '../auth-errors';
import { loginNoticeState } from '../login-notices';

/** The server's answer for an unknown / used / revoked / expired link – shown for a missing token too. */
export const INVALID_LINK_MESSAGE = 'הקישור אינו בתוקף. יש לפנות למנהל המערכת.';
const INVALID_LINK_TYPE = 'invalid-invitation';
export const SET_PASSWORD_PATH = '/set-password';

/**
 * The page of the invitation link: {app}/set-password#token=...
 * - The token is read from the fragment (never sent to a server in a URL) and removed from the address bar at once,
 *   so it is not left in the history, a bookmark or a screenshot. It lives in a private field only (not a signal,
 *   not the DOM, never logged) and is dropped as soon as the server gave a final answer.
 * - No session: a signed-in user is signed out first – the link belongs to another (or a new) account.
 * - Referrer-Policy no-referrer while the page is open.
 * - Success → the login screen with a notice. No automatic sign-in.
 */
@Component({
  selector: 'app-set-password-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AuthBrand,
    NewPasswordFields,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './set-password-page.html',
  styleUrl: './set-password-page.scss',
})
export class SetPasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly meta = inject(Meta);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  /** Not a signal on purpose (see the class comment). */
  private token: string | null;

  protected readonly invalidLinkMessage = INVALID_LINK_MESSAGE;

  protected readonly form = inject(NonNullableFormBuilder).group(newPasswordControls());

  /** False: the link is missing or the server said it is not valid – the form is replaced by the message. */
  protected readonly linkValid = signal(true);
  /** A signed-in user is being signed out before the form can be used. */
  protected readonly signingOut = signal(false);
  protected readonly submitting = signal(false);
  protected readonly alert = signal<FormAlert | null>(null);

  private readonly alertBox = viewChild<ElementRef<HTMLElement>>('alertBox');
  private readonly newPasswordFields = viewChild(NewPasswordFields);

  constructor() {
    this.token = readToken(inject(ActivatedRoute).snapshot.fragment);
    this.linkValid.set(this.token !== null);
    this.removeTokenFromAddressBar();

    this.meta.updateTag({ name: 'referrer', content: 'no-referrer' });
    this.destroyRef.onDestroy(() => this.meta.removeTag("name='referrer'"));

    if (this.auth.isAuthenticated()) {
      this.signingOut.set(true);
      void this.auth.signOut().finally(() => this.signingOut.set(false));
    }
  }

  protected submit(): void {
    if (this.submitting() || this.signingOut() || !this.token) {
      return;
    }
    this.alert.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.newPasswordFields()?.focusFirstInvalid();
      return;
    }

    this.submitting.set(true);
    this.auth
      .setPassword({ token: this.token, newPassword: this.form.controls.newPassword.value })
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          this.token = null;
          this.form.reset();
          void this.router.navigate(['/login'], { state: loginNoticeState('passwordSet'), replaceUrl: true });
        },
        error: (error: unknown) => this.showServerError(error),
      });
  }

  private showServerError(error: unknown): void {
    const type = error instanceof HttpErrorResponse ? problemType(error) : null;

    if (type === INVALID_LINK_TYPE) {
      // Final: the link cannot work any more.
      this.token = null;
      this.form.reset();
      this.linkValid.set(false);
      return;
    }

    if (type === 'password-policy' && error instanceof HttpErrorResponse) {
      const messages = problemMessages(error);
      const title = isProblemDetails(error.error) ? error.error.title : undefined;
      const control = this.form.controls.newPassword;
      control.setErrors({ server: messages.length ? messages.join(' · ') : (title ?? '') });
      control.markAsTouched();
      afterNextRender(() => this.newPasswordFields()?.focus('newPassword'), { injector: this.injector });
      return;
    }

    // Network, 429, 500: the link may still be fine – keep the token so the employee can try again.
    this.alert.set(changePasswordAlert(error));
    afterNextRender(() => this.alertBox()?.nativeElement.focus(), { injector: this.injector });
  }

  /**
   * history.replaceState through Location. Done now and again when the navigation ends: the router writes the URL
   * (with the fragment) at the end of the first navigation, after this component was created.
   */
  private removeTokenFromAddressBar(): void {
    const strip = () => {
      if (this.location.path(true) !== SET_PASSWORD_PATH) {
        this.location.replaceState(SET_PASSWORD_PATH);
      }
    };
    strip();
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        take(1),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(strip);
  }
}

/** "token=abc" (the fragment without '#') → "abc"; null when missing or empty. */
export function readToken(fragment: string | null): string | null {
  if (!fragment) {
    return null;
  }
  const token = new URLSearchParams(fragment).get('token')?.trim();
  return token ? token : null;
}
