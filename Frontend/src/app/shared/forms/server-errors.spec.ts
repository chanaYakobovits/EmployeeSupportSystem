import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, Validators } from '@angular/forms';

import { applyServerErrors, clearServerErrors } from './server-errors';

function problem(status: number, body: Record<string, unknown>): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: { status, title: 'הבקשה אינה תקינה', ...body } });
}

describe('applyServerErrors', () => {
  let form: FormGroup<{
    firstName: FormControl<string>;
    email: FormControl<string>;
    roles: FormGroup<{ Admin: FormControl<boolean> }>;
    manager: FormControl<number | null>;
  }>;

  beforeEach(() => {
    form = new FormGroup({
      firstName: new FormControl('דנה', { nonNullable: true, validators: Validators.required }),
      email: new FormControl('dana@example.test', { nonNullable: true }),
      roles: new FormGroup({ Admin: new FormControl(false, { nonNullable: true }) }),
      manager: new FormControl<number | null>(3),
    });
  });

  it('400: puts each message on its control (camelCase names)', () => {
    const result = applyServerErrors(
      form,
      problem(400, { errors: { firstName: ['יש להזין שם פרטי'], email: ['כתובת המייל אינה תקינה'] } }),
    );

    expect(result).toEqual({ fields: ['firstName', 'email'], unmatched: [] });
    expect(form.controls.firstName.getError('server')).toBe('יש להזין שם פרטי');
    expect(form.controls.email.getError('server')).toBe('כתובת המייל אינה תקינה');
    expect(form.controls.email.touched).toBeTrue();
    expect(form.invalid).toBeTrue();
  });

  it('409: the duplicate e-mail goes next to the e-mail field', () => {
    const result = applyServerErrors(
      form,
      problem(409, {
        title: 'כתובת המייל כבר רשומה במערכת',
        type: 'https://example.test/errors/duplicate-email',
        errors: { email: ['כתובת המייל כבר רשומה במערכת'] },
      }),
    );
    expect(result.fields).toEqual(['email']);
    expect(form.controls.email.getError('server')).toBe('כתובת המייל כבר רשומה במערכת');
  });

  it('maps indexed and differently-cased names ("roles[0]", "Email") to the control', () => {
    applyServerErrors(form, problem(400, { errors: { 'roles[0]': ['התפקיד אינו קיים'], Email: ['שגוי'] } }));
    expect(form.controls.roles.getError('server')).toBe('התפקיד אינו קיים');
    expect(form.controls.email.getError('server')).toBe('שגוי');
  });

  it('uses aliases for server names that differ from the control names', () => {
    const result = applyServerErrors(form, problem(400, { errors: { managerUserId: ['יש לבחור עובד פעיל בארגון'] } }), {
      fieldAliases: { managerUserId: 'manager' },
    });
    expect(result.fields).toEqual(['manager']);
    expect(form.controls.manager.getError('server')).toBe('יש לבחור עובד פעיל בארגון');
  });

  it('maps a ProblemDetails type without "errors" to a field (422 manages-other-department)', () => {
    const result = applyServerErrors(
      form,
      problem(422, { title: 'העובד כבר מנהל את מחלקת כספים', type: 'https://example.test/errors/manages-other-department' }),
      { typeFields: { 'manages-other-department': 'manager' } },
    );
    expect(result).toEqual({ fields: ['manager'], unmatched: [] });
    expect(form.controls.manager.getError('server')).toBe('העובד כבר מנהל את מחלקת כספים');
  });

  it('returns messages that belong to no control', () => {
    const result = applyServerErrors(form, problem(400, { errors: { pageSize: ['ערך לא חוקי'] } }));
    expect(result).toEqual({ fields: [], unmatched: ['ערך לא חוקי'] });

    const list = applyServerErrors(form, problem(400, { errors: ['הסיסמה קצרה מדי'] }));
    expect(list.unmatched).toEqual(['הסיסמה קצרה מדי']);
  });

  it('ignores errors that are not ProblemDetails', () => {
    expect(applyServerErrors(form, new Error('x'))).toEqual({ fields: [], unmatched: [] });
    expect(applyServerErrors(form, new HttpErrorResponse({ status: 0 }))).toEqual({ fields: [], unmatched: [] });
  });

  it('the server error disappears when the user edits the field, and keeps the other errors', () => {
    applyServerErrors(form, problem(409, { errors: { email: ['כבר רשום'] } }));
    form.controls.email.setValue('other@example.test');
    expect(form.controls.email.hasError('server')).toBeFalse();

    form.controls.firstName.setValue('');
    applyServerErrors(form, problem(400, { errors: { firstName: ['x'] } }));
    clearServerErrors(form);
    expect(form.controls.firstName.hasError('server')).toBeFalse();
    expect(form.controls.firstName.hasError('required')).toBeTrue();
  });
});
