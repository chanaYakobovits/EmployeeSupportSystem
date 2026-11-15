import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';

import { isProblemDetails, problemType } from '../models/problem-details';

/** The validation error key that holds a server message (shown by <mat-error> like any other error). */
export const SERVER_ERROR = 'server';

export interface ServerErrorOptions {
  /** Server field name → control name, when they differ (e.g. managerUserId → manager). */
  fieldAliases?: Readonly<Record<string, string>>;
  /**
   * ProblemDetails type → control name, for errors about one field that the server reports without "errors"
   * (e.g. a 422 manages-other-department belongs next to the manager field). The title is the message.
   */
  typeFields?: Readonly<Record<string, string>>;
  /**
   * Server fields this form shows elsewhere (e.g. "files" – see attachmentServerError): their messages are neither put on
   * a control nor returned as unmatched.
   */
  skipFields?: readonly string[];
}

export interface AppliedServerErrors {
  /** Controls that received a server error, in form order – focus the first one. */
  fields: string[];
  /** Messages that are not about a control of this form – show them in an alert. */
  unmatched: string[];
}

/**
 * Puts the field errors of a ProblemDetails (400 validation, 409 duplicate e-mail, …) on the matching controls
 * of the form, as { server: message }. Field names are camelCase as in the request body; "roles[0]" → "roles".
 * The error disappears as soon as the user changes the value (the validators run again).
 */
export function applyServerErrors(form: FormGroup, error: unknown, options: ServerErrorOptions = {}): AppliedServerErrors {
  const result: AppliedServerErrors = { fields: [], unmatched: [] };
  if (!(error instanceof HttpErrorResponse) || !isProblemDetails(error.error)) {
    return result;
  }
  const problem = error.error;
  const applied = new Set<string>();

  const type = problemType(error);
  const typeField = type ? options.typeFields?.[type] : undefined;
  if (typeField && problem.title && setServerError(form, typeField, problem.title)) {
    applied.add(typeField);
  }

  const errors = problem.errors;
  if (Array.isArray(errors)) {
    result.unmatched.push(...errors);
  } else if (errors) {
    for (const [field, messages] of Object.entries(errors)) {
      if (options.skipFields?.some((skip) => baseField(field).toLowerCase() === skip.toLowerCase())) {
        continue;
      }
      const name = controlName(form, field, options.fieldAliases);
      if (name && setServerError(form, name, messages.join(' '))) {
        applied.add(name);
      } else {
        result.unmatched.push(...messages);
      }
    }
  }

  result.fields = Object.keys(form.controls).filter((name) => applied.has(name));
  return result;
}

/** Removes the server errors of every control, e.g. before submitting again. */
export function clearServerErrors(form: FormGroup): void {
  for (const control of Object.values(form.controls)) {
    if (control.hasError(SERVER_ERROR)) {
      const { [SERVER_ERROR]: _removed, ...rest } = control.errors ?? {};
      control.setErrors(Object.keys(rest).length ? rest : null);
    }
  }
}

function setServerError(form: FormGroup, name: string, message: string): boolean {
  const control: AbstractControl | undefined = form.controls[name];
  if (!control || control.disabled) {
    return false;
  }
  control.setErrors({ ...(control.errors ?? {}), [SERVER_ERROR]: message });
  control.markAsTouched();
  return true;
}

/** "Email", "$.email", "roles[0]", "address.city" → the top-level control name, case-insensitive. */
function controlName(form: FormGroup, field: string, aliases?: Readonly<Record<string, string>>): string | null {
  const base = baseField(field);
  const aliased = aliases?.[base] ?? aliases?.[field] ?? base;
  const lower = aliased.toLowerCase();
  return Object.keys(form.controls).find((name) => name.toLowerCase() === lower) ?? null;
}

/** "$.files[1]" → "files". */
function baseField(field: string): string {
  return field.replace(/^\$\./, '').split(/[.[]/)[0];
}

/** The server's field name of the attached files (multipart "files"; errors on "files" and "files[i]"). */
export const FILES_FIELD = 'files';

/**
 * The server's errors about the attached files, as one message for the file picker: 400 on "files" / "files[i]"
 * ("files[1]" → "<name of the 2nd file>: message"), or 413 (the whole request is too large). Null otherwise.
 */
export function attachmentServerError(error: unknown, files: readonly File[]): string | null {
  if (!(error instanceof HttpErrorResponse)) {
    return null;
  }
  const problem = isProblemDetails(error.error) ? error.error : null;
  if (error.status === 413) {
    const title = problem?.title ?? 'גודל הבקשה חורג מהמותר';
    return problem?.detail ? `${title}. ${problem.detail}` : title;
  }
  const errors = problem?.errors;
  if (!errors || Array.isArray(errors)) {
    return null;
  }
  const messages: string[] = [];
  for (const [field, fieldMessages] of Object.entries(errors)) {
    const match = /^files(?:\[(\d+)\])?$/i.exec(field.replace(/^\$\./, ''));
    if (!match) {
      continue;
    }
    const fileName = match[1] === undefined ? undefined : files[Number(match[1])]?.name;
    messages.push(...fieldMessages.map((message) => (fileName ? `${fileName}: ${message}` : message)));
  }
  return messages.length > 0 ? messages.join(' ') : null;
}
