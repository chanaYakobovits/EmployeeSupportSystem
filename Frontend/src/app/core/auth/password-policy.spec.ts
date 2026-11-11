import { FormControl } from '@angular/forms';

import { brokenPasswordRules, passwordPolicyValidator, passwordStrength } from './password-policy';

describe('password policy (spec 6.1)', () => {
  it('accepts 8+ characters with a letter and a digit', () => {
    expect(brokenPasswordRules('abcdefg1')).toEqual([]);
  });

  it('reports every broken rule', () => {
    expect(brokenPasswordRules('abc')).toEqual(['minLength', 'digit']);
    expect(brokenPasswordRules('12345678')).toEqual(['letter']);
  });

  it('counts Hebrew letters as letters (same as the server)', () => {
    expect(brokenPasswordRules('שלוםעולם1')).toEqual([]);
  });

  it('validator: empty is left to "required", too long is reported', () => {
    expect(passwordPolicyValidator(new FormControl(''))).toBeNull();
    expect(passwordPolicyValidator(new FormControl('a1'.repeat(65)))).toEqual(
      jasmine.objectContaining({ maxlength: jasmine.anything() }),
    );
    expect(passwordPolicyValidator(new FormControl('short1'))).toEqual({ passwordPolicy: ['minLength'] });
  });

  it('strength grows with length and variety', () => {
    expect(passwordStrength('')).toBe(0);
    expect(passwordStrength('abc')).toBe(1);
    expect(passwordStrength('abcdefg1')).toBe(2);
    expect(passwordStrength('Abcdefghij1!')).toBe(4);
  });
});
