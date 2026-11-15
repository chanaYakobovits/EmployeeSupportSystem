import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Subject, of, throwError } from 'rxjs';

import { TicketsApiService } from '../../../services/tickets-api.service';
import { TicketCreated } from '../../../models/ticket.models';
import { NotificationService } from '../../../services/notification.service';
import { ConfirmService } from '../../../services/confirm.service';
import { unsavedChangesGuard } from '../../../guards/unsaved-changes.guard';
import { NewTicketPage } from './new-ticket-page';
import { fakeFile } from '../testing/ticket-fixtures';

@Component({ template: '' })
class Stub {}

describe('NewTicketPage', () => {
  let api: jasmine.SpyObj<TicketsApiService>;
  let notifications: jasmine.SpyObj<NotificationService>;
  let confirm: jasmine.SpyObj<ConfirmService>;
  let harness: RouterTestingHarness;
  let element: HTMLElement;

  beforeEach(async () => {
    api = jasmine.createSpyObj<TicketsApiService>('TicketsApiService', ['create']);
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'error']);
    confirm = jasmine.createSpyObj<ConfirmService>('ConfirmService', ['confirm']);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'employee', component: Stub },
          { path: 'employee/tickets/new', component: NewTicketPage, canDeactivate: [unsavedChangesGuard] },
          { path: 'employee/tickets/:id', component: Stub },
        ]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: TicketsApiService, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: ConfirmService, useValue: confirm },
      ],
    });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/employee/tickets/new', NewTicketPage);
    harness.detectChanges();
    element = harness.routeNativeElement!;
  });

  function type(selector: string, value: string): void {
    const field = element.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    harness.detectChanges();
  }

  function submitButton(): HTMLButtonElement {
    return element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
  }

  function submit(): void {
    submitButton().click();
    harness.detectChanges();
  }

  const router = () => TestBed.inject(Router);

  async function pick(...files: File[]): Promise<void> {
    const input = element.querySelector<HTMLInputElement>('input[type="file"]')!;
    const transfer = new DataTransfer();
    files.forEach((file) => transfer.items.add(file));
    input.files = transfer.files;
    input.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();
  }

  function problem(status: number, body: Record<string, unknown>): HttpErrorResponse {
    return new HttpErrorResponse({ status, error: { title: 'הבקשה אינה תקינה', status, ...body } });
  }

  it('shows the fixed privacy note', () => {
    expect(element.textContent).toContain(
      'אין צורך לכתוב פרטים אישיים כמו תעודת זהות או מספר חשבון – הפנייה מזוהה אוטומטית.',
    );
  });

  it('counts the description characters', () => {
    type('textarea', 'שלום עולם');
    expect(element.textContent).toContain('9 / 4,000');
  });

  it('does not send a description shorter than 10 characters', () => {
    type('textarea', 'קצר');
    submit();
    expect(api.create).not.toHaveBeenCalled();
    expect(element.textContent).toContain('התיאור צריך להכיל לפחות 10 תווים');
  });

  it('disables the send button while sending (no double submit)', () => {
    api.create.and.returnValue(new Subject<TicketCreated>());
    type('textarea', 'המדפסת בקומה 2 לא מדפיסה');
    submit();

    expect(submitButton().disabled).toBeTrue();
    submitButton().click();
    expect(api.create).toHaveBeenCalledTimes(1);
  });

  it('sends trimmed text, an empty subject as null', () => {
    api.create.and.returnValue(new Subject<TicketCreated>());
    type('input', '   ');
    type('textarea', '  המדפסת לא מדפיסה  ');
    submit();
    expect(api.create).toHaveBeenCalledWith({ subject: null, description: 'המדפסת לא מדפיסה', files: [] });
  });

  it('after success: a message with the number, then the ticket view – without asking to confirm', async () => {
    api.create.and.returnValue(of({ id: 42 }));
    type('textarea', 'המדפסת בקומה 2 לא מדפיסה');
    submit();
    await harness.fixture.whenStable();

    expect(notifications.success).toHaveBeenCalledWith("הפנייה התקבלה, מס' 42");
    expect(router().url).toBe('/employee/tickets/42');
    expect(confirm.confirm).not.toHaveBeenCalled();
  });

  it('a validation error from the server is shown next to the field', () => {
    api.create.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { title: 'הבקשה אינה תקינה', status: 400, errors: { description: ['התיאור ארוך מדי'] } },
          }),
      ),
    );
    type('textarea', 'המדפסת בקומה 2 לא מדפיסה');
    submit();
    expect(element.querySelector('mat-error')?.textContent).toContain('התיאור ארוך מדי');
    expect(element.querySelector('app-form-alert')).toBeNull();
  });

  it('429: an alert above the form with the server\'s explanation, the text is kept', () => {
    api.create.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 429,
            error: {
              title: 'בוצעו יותר מדי בקשות',
              status: 429,
              detail: 'ניתן לפתוח עד 20 פניות בשעה. יש לנסות שוב מאוחר יותר.',
            },
          }),
      ),
    );
    type('textarea', 'המדפסת בקומה 2 לא מדפיסה');
    submit();
    const alert = element.querySelector('app-form-alert')!;
    expect(alert.textContent).toContain('ניתן לפתוח עד 20 פניות בשעה');
    expect(element.querySelector('textarea')!.value).toBe('המדפסת בקומה 2 לא מדפיסה');
    expect(submitButton().disabled).toBeFalse();
  });

  describe('attachments', () => {
    it('the chosen files are sent with the fields', async () => {
      api.create.and.returnValue(new Subject<TicketCreated>());
      const files = [fakeFile('תלוש.pdf'), fakeFile('צילום.png')];
      await pick(...files);
      type('textarea', 'חסרות שעות נוספות בתלוש');
      submit();
      expect(api.create).toHaveBeenCalledOnceWith({ subject: null, description: 'חסרות שעות נוספות בתלוש', files });
    });

    it('a file that is not allowed is rejected before sending', async () => {
      await pick(fakeFile('setup.exe'));
      expect(element.querySelector('app-file-upload .messages')!.textContent).toContain(
        'ניתן לצרף רק קבצי PDF, JPG, PNG או DOCX',
      );
    });

    it('400 on files[1]: shown next to the file picker with the file name, not in an alert', async () => {
      api.create.and.returnValue(
        throwError(() => problem(400, { errors: { 'files[1]': ['תוכן הקובץ אינו תואם לסוג הקובץ'] } })),
      );
      await pick(fakeFile('תלוש.pdf'), fakeFile('צילום.png'));
      type('textarea', 'חסרות שעות נוספות בתלוש');
      submit();

      const messages = element.querySelector('app-file-upload .messages')!.textContent!;
      expect(messages).toContain('צילום.png: תוכן הקובץ אינו תואם לסוג הקובץ');
      expect(element.querySelector('app-form-alert')).toBeNull();
      await harness.fixture.whenStable();
      expect(document.activeElement).toBe(element.querySelector('app-file-upload .choose-button'));
    });

    it('413: shown next to the file picker', async () => {
      api.create.and.returnValue(
        throwError(() =>
          problem(413, { title: 'גודל הבקשה חורג מהמותר', detail: 'ניתן לצרף עד 3 קבצים, עד 5MB כל אחד.' }),
        ),
      );
      await pick(fakeFile('תלוש.pdf'));
      type('textarea', 'חסרות שעות נוספות בתלוש');
      submit();
      expect(element.querySelector('app-file-upload .messages')!.textContent).toContain('גודל הבקשה חורג מהמותר');
      expect(element.querySelector('app-form-alert')).toBeNull();
    });
  });

  describe('leaving the screen', () => {
    it('files chosen without any text: asks to confirm, about the files', async () => {
      confirm.confirm.and.returnValue(of(false));
      await pick(fakeFile('תלוש.pdf'));
      const left = await router().navigateByUrl('/employee');
      expect(left).toBeFalse();
      expect(confirm.confirm).toHaveBeenCalledOnceWith(jasmine.objectContaining({ message: 'הקבצים שבחרת לא נשלחו.' }));
    });

    it('text and files: the message names both', async () => {
      confirm.confirm.and.returnValue(of(false));
      await pick(fakeFile('תלוש.pdf'));
      type('textarea', 'התחלתי לכתוב');
      await router().navigateByUrl('/employee');
      expect(confirm.confirm).toHaveBeenCalledOnceWith(
        jasmine.objectContaining({ message: 'הטקסט והקבצים שבחרת לא נשמרו ויימחקו.' }),
      );
    });

    it('with text typed: asks to confirm, and stays when the user cancels', async () => {
      confirm.confirm.and.returnValue(of(false));
      type('textarea', 'התחלתי לכתוב');
      const left = await router().navigateByUrl('/employee');
      expect(confirm.confirm).toHaveBeenCalledTimes(1);
      expect(left).toBeFalse();
      expect(router().url).toBe('/employee/tickets/new');
    });

    it('with text typed: leaves when the user confirms', async () => {
      confirm.confirm.and.returnValue(of(true));
      type('input', 'נושא בלבד');
      const left = await router().navigateByUrl('/employee');
      expect(left).toBeTrue();
      expect(router().url).toBe('/employee');
    });

    it('nothing typed (or only spaces): leaves without asking', async () => {
      type('textarea', '   ');
      const left = await router().navigateByUrl('/employee');
      expect(confirm.confirm).not.toHaveBeenCalled();
      expect(left).toBeTrue();
    });
  });
});
