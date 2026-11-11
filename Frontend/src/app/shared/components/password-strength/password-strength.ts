import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { PasswordStrength, passwordStrength } from '../../../core/auth/password-policy';

const LABELS: Record<PasswordStrength, string> = {
  0: '',
  1: 'חלשה',
  2: 'בינונית',
  3: 'טובה',
  4: 'חזקה',
};

/** Strength meter for a new password (spec 7.2). Guidance only – the policy rules decide acceptance. */
@Component({
  selector: 'app-password-strength',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="track" aria-hidden="true">
      <span class="bar" [attr.data-level]="strength()" [style.width.%]="strength() * 25"></span>
    </div>
    <p class="label" aria-live="polite">
      @if (strength()) {
        חוזק הסיסמה: <span [attr.data-level]="strength()">{{ label() }}</span>
      }
    </p>
  `,
  styles: `
    :host { display: block; margin: 0 0 8px; }
    .track { height: 6px; border-radius: 3px; background: var(--border); overflow: hidden; }
    .bar { display: block; height: 100%; transition: width var(--transition), background-color var(--transition); }
    .label { margin: 6px 0 0; min-height: 20px; font-size: 13px; color: var(--text-secondary); }
    [data-level='1'] { background-color: var(--error-text); color: var(--error-text); }
    [data-level='2'] { background-color: var(--warning-text); color: var(--warning-text); }
    [data-level='3'] { background-color: var(--primary); color: var(--primary); }
    [data-level='4'] { background-color: var(--success-text); color: var(--success-text); }
    .label span { background: none; font-weight: 600; }
  `,
})
export class PasswordStrengthMeter {
  readonly password = input('');

  protected readonly strength = computed(() => passwordStrength(this.password()));
  protected readonly label = computed(() => LABELS[this.strength()]);
}
