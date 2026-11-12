import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, ControlContainer, FormGroupDirective, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { EMPTY, Observable, merge } from 'rxjs';

import { PASSWORD_RULES } from '../../../core/auth/password-policy';
import { PasswordStrengthMeter } from '../password-strength/password-strength';

type FieldName = 'newPassword' | 'confirmPassword';
type RuleState = 'pending' | 'met' | 'unmet';

let nextId = 0;

/**
 * "New password + confirmation" (spec 6.1, 7.2): both fields with show/hide, the policy rules shown up front and
 * checked live, and the strength meter. Used by change-password and set-password.
 *
 * Lives inside the parent's form: the parent's [formGroup] must have controls "newPassword" and
 * "confirmPassword" (see newPasswordControls in core/auth/password-policy.ts). The controls bind with
 * formControlName through the parent's ControlContainer, so the parent's submit state shows the errors too.
 * Known error keys: required, passwordPolicy, maxlength, sameAsCurrent, mismatch, server (a message string).
 */
@Component({
  selector: 'app-new-password-fields',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, PasswordStrengthMeter],
  viewProviders: [{ provide: ControlContainer, useFactory: () => inject(ControlContainer, { skipSelf: true }) }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './new-password-fields.html',
  styleUrl: './new-password-fields.scss',
})
export class NewPasswordFields implements OnInit {
  private readonly container = inject(ControlContainer, { skipSelf: true });
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetector = inject(ChangeDetectorRef);

  readonly newPasswordLabel = input('סיסמה חדשה');
  readonly confirmPasswordLabel = input('אימות סיסמה חדשה');

  /** Unique per instance, for aria-describedby. */
  protected readonly rulesId = `password-rules-${nextId++}`;

  protected readonly visible = signal<Record<FieldName, boolean>>({ newPassword: false, confirmPassword: false });
  protected readonly newPassword = signal('');

  protected readonly rules = computed(() => {
    const value = this.newPassword();
    return PASSWORD_RULES.map((rule) => ({
      id: rule.id,
      label: rule.label,
      state: (!value ? 'pending' : rule.test(value) ? 'met' : 'unmet') as RuleState,
    }));
  });

  private readonly newInput = viewChild.required<ElementRef<HTMLInputElement>>('newInput');
  private readonly confirmInput = viewChild.required<ElementRef<HTMLInputElement>>('confirmInput');

  // The parent's [formGroup] input is set before this runs.
  ngOnInit(): void {
    const newPassword = this.control('newPassword');
    const confirmPassword = this.control('confirmPassword');

    // OnPush: the parent's submit or markAllAsTouched / setErrors changes what these fields must show, without any
    // event inside this view. Re-check on every control event (bubbles up from the controls) and on submit.
    const submits: Observable<unknown> =
      this.container instanceof FormGroupDirective ? this.container.ngSubmit : EMPTY;
    const controlEvents: Observable<unknown> = this.container.control!.events;
    merge(controlEvents, submits)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.changeDetector.markForCheck());

    this.newPassword.set((newPassword.value as string | null) ?? '');
    newPassword.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value: string | null) => {
      this.newPassword.set(value ?? '');
      // Cross-field rule: the confirmation depends on the new password.
      if (confirmPassword.dirty) {
        confirmPassword.updateValueAndValidity({ emitEvent: false });
      }
    });
  }

  protected control(name: FieldName): AbstractControl {
    const control = this.container.control?.get(name);
    if (!control) {
      throw new Error(`app-new-password-fields: the parent form has no "${name}" control.`);
    }
    return control;
  }

  protected isVisible(field: FieldName): boolean {
    return this.visible()[field];
  }

  protected toggleVisible(field: FieldName): void {
    this.visible.update((state) => ({ ...state, [field]: !state[field] }));
  }

  /** Focuses the first invalid field of the two. Returns false if both are valid. */
  focusFirstInvalid(): boolean {
    const field = (['newPassword', 'confirmPassword'] as const).find((name) => this.control(name).invalid);
    if (!field) {
      return false;
    }
    this.focus(field);
    return true;
  }

  focus(field: FieldName): void {
    (field === 'newPassword' ? this.newInput() : this.confirmInput()).nativeElement.focus();
  }
}
