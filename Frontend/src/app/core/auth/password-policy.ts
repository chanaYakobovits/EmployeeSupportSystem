import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Spec 6.1 password policy – the same rules as Backend Services PasswordPolicy, checked here too so the user
 * sees them before submitting. The server stays the authority.
 */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;

export type PasswordRuleId = 'minLength' | 'letter' | 'digit';

export interface PasswordRule {
  id: PasswordRuleId;
  label: string;
  test: (value: string) => boolean;
}

/** Any script counts as a letter (Hebrew too), as on the server (char.IsLetter). */
const LETTER = /\p{L}/u;
const DIGIT = /\p{Nd}/u;

export const PASSWORD_RULES: readonly PasswordRule[] = [
  { id: 'minLength', label: `לפחות ${PASSWORD_MIN_LENGTH} תווים`, test: (v) => v.length >= PASSWORD_MIN_LENGTH },
  { id: 'letter', label: 'לפחות אות אחת', test: (v) => LETTER.test(v) },
  { id: 'digit', label: 'לפחות ספרה אחת', test: (v) => DIGIT.test(v) },
];

/** Ids of the rules the value breaks (empty = acceptable). */
export function brokenPasswordRules(value: string): PasswordRuleId[] {
  return PASSWORD_RULES.filter((rule) => !rule.test(value)).map((rule) => rule.id);
}

/** Reactive Forms validator: { passwordPolicy: PasswordRuleId[] } or { maxlength }. */
export const passwordPolicyValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = (control.value as string | null) ?? '';
  if (!value) {
    return null; // "required" reports an empty field
  }
  if (value.length > PASSWORD_MAX_LENGTH) {
    return { maxlength: { requiredLength: PASSWORD_MAX_LENGTH, actualLength: value.length } };
  }
  const broken = brokenPasswordRules(value);
  return broken.length ? { passwordPolicy: broken } : null;
};

export type PasswordStrength = 0 | 1 | 2 | 3 | 4;

/**
 * A simple, dependency-free strength estimate for the meter (0 = empty, 1 weak … 4 strong).
 * It is guidance only – acceptance is decided by the policy rules.
 */
export function passwordStrength(value: string): PasswordStrength {
  if (!value) {
    return 0;
  }
  if (brokenPasswordRules(value).length) {
    return 1;
  }
  let score = 0;
  if (value.length >= 12) score++;
  if (/\p{Lu}/u.test(value) && /\p{Ll}/u.test(value)) score++;
  if (/[^\p{L}\p{Nd}]/u.test(value)) score++;
  if (value.length >= 16) score++;
  return score === 0 ? 2 : score === 1 ? 3 : 4;
}
