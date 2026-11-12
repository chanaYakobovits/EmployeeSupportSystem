import { FormControl, FormGroup, Validators } from '@angular/forms';

import { SLA_ORDER_ERROR, formatHours, slaOrderMessage, slaOrderValidator } from './sla-rules';

describe('slaOrderValidator', () => {
  function form(critical: number | null, high: number | null, medium: number | null, low: number | null) {
    return new FormGroup(
      {
        critical: new FormControl(critical, Validators.required),
        high: new FormControl(high, Validators.required),
        medium: new FormControl(medium, Validators.required),
        low: new FormControl(low, Validators.required),
      },
      { validators: [slaOrderValidator] },
    );
  }

  it('accepts ascending and equal values', () => {
    expect(form(4, 24, 72, 120).valid).toBeTrue();
    expect(form(24, 24, 24, 24).valid).toBeTrue();
  });

  it('puts the error on the less urgent level, with the server wording', () => {
    const group = form(25, 24, 72, 120);

    expect(group.valid).toBeFalse();
    expect(group.controls.high.getError(SLA_ORDER_ERROR)).toBe(slaOrderMessage('High', 'Critical'));
    expect(group.controls.high.getError(SLA_ORDER_ERROR)).toBe(
      'היעד לדחיפות גבוהה לא יכול להיות קצר מהיעד לדחיפות קריטית',
    );
    expect(group.controls.critical.errors).toBeNull();
  });

  it('reports each pair out of order', () => {
    const group = form(4, 80, 72, 48);

    expect(group.controls.medium.hasError(SLA_ORDER_ERROR)).toBeTrue();
    expect(group.controls.low.hasError(SLA_ORDER_ERROR)).toBeTrue();
    expect(group.controls.high.hasError(SLA_ORDER_ERROR)).toBeFalse();
  });

  it('removes the error once the order is fixed', () => {
    const group = form(25, 24, 72, 120);
    group.controls.critical.setValue(4);

    expect(group.valid).toBeTrue();
    expect(group.controls.high.errors).toBeNull();
  });

  it('keeps the control own errors and skips pairs with a missing value', () => {
    const group = form(4, null, 72, 120);

    expect(group.controls.high.hasError('required')).toBeTrue();
    expect(group.controls.high.hasError(SLA_ORDER_ERROR)).toBeFalse();
    expect(group.controls.medium.hasError(SLA_ORDER_ERROR)).toBeFalse();
  });
});

describe('formatHours', () => {
  it('shows hours under a day as hours only', () => {
    expect(formatHours(4)).toBe('4 שעות');
    expect(formatHours(1)).toBe('שעה אחת');
  });

  it('adds days for whole days', () => {
    expect(formatHours(24)).toBe('24 שעות = יום אחד');
    expect(formatHours(48)).toBe('48 שעות = יומיים');
    expect(formatHours(72)).toBe('72 שעות = 3 ימים');
    expect(formatHours(120)).toBe('120 שעות = 5 ימים');
  });

  it('adds days and the remaining hours', () => {
    expect(formatHours(30)).toBe('30 שעות = יום ו-6 שעות');
    expect(formatHours(49)).toBe('49 שעות = יומיים ושעה');
  });

  it('shows nothing for an empty or invalid value', () => {
    expect(formatHours(null)).toBe('');
    expect(formatHours(0)).toBe('');
    expect(formatHours(1.5)).toBe('');
  });
});
