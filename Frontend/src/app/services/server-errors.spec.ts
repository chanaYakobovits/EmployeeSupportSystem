import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, Validators } from '@angular/forms';

import { fakeFile } from '../components/employee/testing/ticket-fixtures';
import { applyServerErrors, attachmentServerError, clearServerErrors } from './server-errors';

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

describe('applyServerErrors – skipFields', () => {
  it('neither applies nor returns the messages of a skipped field', () => {
    const form = new FormGroup({ body: new FormControl('', { nonNullable: true }), files: new FormControl([]) });
    const result = applyServerErrors(
      form,
      problem(400, { errors: { body: ['יש לכתוב את התגובה'], 'files[0]': ['הקובץ ריק'], files: ['ניתן לצרף עד 3 קבצים'] } }),
      { skipFields: ['files'] },
    );
    expect(result.fields).toEqual(['body']);
    expect(result.unmatched).toEqual([]);
    expect(form.controls.files.errors).toBeNull();
  });
});

describe('attachmentServerError', () => {
  const files = [fakeFile('תלוש.pdf'), fakeFile('צילום.png')];

  it('400 files[i]: the message with the name of that file', () => {
    const error = problem(400, { errors: { 'files[1]': ['תוכן הקובץ אינו תואם לסוג הקובץ'] } });
    expect(attachmentServerError(error, files)).toBe('צילום.png: תוכן הקובץ אינו תואם לסוג הקובץ');
  });

  it('400 files: the message alone', () => {
    const error = problem(400, { errors: { files: ['ניתן לצרף עד 3 קבצים'], description: ['x'] } });
    expect(attachmentServerError(error, files)).toBe('ניתן לצרף עד 3 קבצים');
  });

  it('413: the title and the explanation', () => {
    const error = problem(413, { title: 'גודל הבקשה חורג מהמותר', detail: 'ניתן לצרף עד 3 קבצים, עד 5MB כל אחד.' });
    expect(attachmentServerError(error, files)).toBe('גודל הבקשה חורג מהמותר. ניתן לצרף עד 3 קבצים, עד 5MB כל אחד.');
  });

  it('413 without a body (e.g. from a proxy)', () => {
    expect(attachmentServerError(new HttpErrorResponse({ status: 413 }), files)).toBe('גודל הבקשה חורג מהמותר');
  });

  it('nothing about the files → null', () => {
    expect(attachmentServerError(problem(400, { errors: { body: ['x'] } }), files)).toBeNull();
    expect(attachmentServerError(new HttpErrorResponse({ status: 500 }), files)).toBeNull();
  });
});
