import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { EMPTY, Subject, of, throwError } from 'rxjs';

import { unsavedChangesGuard } from '../../../guards/unsaved-changes.guard';
import { CommentCreated } from '../../../models/ticket.models';
import { AttachmentDownloadService } from '../../../services/attachment-download.service';
import { ConfirmService } from '../../../services/confirm.service';
import { NotificationService } from '../../../services/notification.service';
import { TicketsApiService } from '../../../services/tickets-api.service';
import { fakeAttachment, fakeComment, fakeFile, fakeHistory, fakeTicketDetails } from '../testing/ticket-fixtures';
import { TicketViewPage } from './ticket-view-page';
import { AuthService } from '../../../services/auth.service';
import { fakeAuth, fakeStaffDetails } from '../../handler/testing/handler-fixtures';

@Component({ template: '' })
class Stub {}

const CLOSED_NOTE = 'הפנייה סגורה ולא ניתן להוסיף תגובות';

describe('TicketViewPage', () => {
  let api: jasmine.SpyObj<TicketsApiService>;
  let notifications: jasmine.SpyObj<NotificationService>;
  let confirm: jasmine.SpyObj<ConfirmService>;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    api = jasmine.createSpyObj<TicketsApiService>('TicketsApiService', [
      'get',
      'history',
      'addComment',
      'addAttachments',
      'changeStatus',
    ]);
    api.get.and.returnValue(of(fakeTicketDetails()));
    api.history.and.returnValue(of(fakeHistory()));
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'error']);
    confirm = jasmine.createSpyObj<ConfirmService>('ConfirmService', ['confirm']);
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'employee', component: Stub },
            { path: 'handler/tickets/:id', component: Stub },
            { path: 'employee/tickets/:id', component: TicketViewPage, canDeactivate: [unsavedChangesGuard] },
          ],
          withComponentInputBinding(),
        ),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: TicketsApiService, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: ConfirmService, useValue: confirm },
        { provide: AttachmentDownloadService, useValue: { download: () => EMPTY } },
        { provide: AuthService, useValue: fakeAuth() },
      ],
    });
  });

  it('a ticket the user handles (the staff view) opens on the handling screen', async () => {
    api.get.and.returnValue(of(fakeStaffDetails({ id: 1042 })));
    await open();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/handler/tickets/1042');
  });

  async function open(url = '/employee/tickets/1042'): Promise<HTMLElement> {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, TicketViewPage);
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  function type(element: HTMLElement, value: string): void {
    const field = element.querySelector<HTMLTextAreaElement>('app-ticket-reply textarea')!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
    harness.detectChanges();
  }

  async function pick(element: HTMLElement, ...files: File[]): Promise<void> {
    const input = element.querySelector<HTMLInputElement>('app-ticket-reply input[type="file"]')!;
    const transfer = new DataTransfer();
    files.forEach((file) => transfer.items.add(file));
    input.files = transfer.files;
    input.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();
  }

  function send(element: HTMLElement): void {
    element.querySelector<HTMLButtonElement>('app-ticket-reply button[type="submit"]')!.click();
    harness.detectChanges();
  }

  it('shows the number, subject, status, and "מתבצע שיוך..." / "טרם שויך" before classification', async () => {
    const element = await open();

    expect(api.get).toHaveBeenCalledWith(1042);
    expect(api.history).toHaveBeenCalledWith(1042);
    expect(element.querySelector('h1')!.textContent).toContain('המחשב לא נדלק');
    expect(element.textContent).toContain('#1042');
    expect(element.querySelector('app-status-badge')!.textContent).toContain('חדשה');
    const details = element.querySelector('.details')!.textContent!;
    expect(details).toContain('מתבצע שיוך...');
    expect(details).toContain('טרם שויך');
    expect(details).toContain('10/11/2026 10:00');
  });

  it('the description is plain text (not HTML), with its line breaks', async () => {
    const description = '<b>מודגש</b><img src="x" onerror="alert(1)">\nשורה שנייה';
    api.get.and.returnValue(of(fakeTicketDetails({ description })));
    const element = await open();

    const shown = element.querySelector<HTMLElement>('.description')!;
    expect(shown.querySelector('b')).toBeNull();
    expect(shown.querySelector('img')).toBeNull();
    expect(shown.textContent).toBe(description);
    expect(getComputedStyle(shown).whiteSpace).toBe('pre-wrap');
  });

  it('the timeline comes from /history: status changes, files and comments, as received', async () => {
    api.history.and.returnValue(
      of(
        fakeHistory([
          { kind: 'status', at: '2026-11-10T08:00:00Z', previousStatus: null, newStatus: 'New', changedByName: 'ישראל ישראלי' },
          { kind: 'attachment', at: '2026-11-10T08:00:00Z', attachment: fakeAttachment({ fileName: 'תלוש.pdf' }) },
          { kind: 'status', at: '2026-11-10T08:01:00Z', previousStatus: 'New', newStatus: 'Assigned', changedByName: null },
          fakeComment({ body: 'אפשר צילום מסך?' }),
        ]),
      ),
    );
    const element = await open();
    const entries = Array.from(element.querySelectorAll('app-timeline ol > li')).map((li) => li.textContent!);
    expect(entries.length).toBe(4);
    expect(entries[0]).toContain('הפנייה נפתחה');
    expect(entries[1]).toContain('תלוש.pdf');
    expect(entries[2]).toContain('שויכה');
    expect(entries[2]).toContain('מערכת');
    expect(entries[3]).toContain('אפשר צילום מסך?');
  });

  it('a comment is not shown as HTML', async () => {
    api.history.and.returnValue(of(fakeHistory([fakeComment({ body: '<script>alert(1)</script><b>x</b>' })])));
    const element = await open();
    const body = element.querySelector('app-timeline .body')!;
    expect(body.querySelector('b, script')).toBeNull();
    expect(body.textContent).toBe('<script>alert(1)</script><b>x</b>');
  });

  it('404: "הפנייה לא נמצאה" with a way back to the list', async () => {
    api.get.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    const element = await open('/employee/tickets/999');
    const empty = element.querySelector('app-empty-state')!;
    expect(empty.textContent).toContain('הפנייה לא נמצאה');
    expect(empty.querySelector('a')!.getAttribute('href')).toBe('/employee');
  });

  it('an invalid id is "not found" without calling the server', async () => {
    const element = await open('/employee/tickets/abc');
    expect(api.get).not.toHaveBeenCalled();
    expect(api.history).not.toHaveBeenCalled();
    expect(element.textContent).toContain('הפנייה לא נמצאה');
  });

  it('another error: the error state with "נסה שוב"', async () => {
    api.history.and.returnValues(throwError(() => new HttpErrorResponse({ status: 500 })), of(fakeHistory()));
    const element = await open();
    element.querySelector<HTMLButtonElement>('app-error-state button')!.click();
    harness.detectChanges();
    expect(api.get).toHaveBeenCalledTimes(2);
    expect(element.querySelector('h1')!.textContent).toContain('המחשב לא נדלק');
  });

  describe('the reply', () => {
    it('has a visible label and a counter', async () => {
      const element = await open();
      expect(element.querySelector('app-ticket-reply mat-label')!.textContent).toContain('התגובה שלך');
      type(element, 'שלום');
      expect(element.querySelector('app-ticket-reply')!.textContent).toContain('4 / 4,000');
    });

    it('empty (or only spaces) and no files: nothing is sent, an error next to the field', async () => {
      const element = await open();
      type(element, '   ');
      send(element);
      expect(api.addComment).not.toHaveBeenCalled();
      expect(api.addAttachments).not.toHaveBeenCalled();
      expect(element.querySelector('app-ticket-reply mat-error')!.textContent).toContain('יש לכתוב תגובה או לצרף קובץ');
    });

    it('text: a comment with the trimmed text and the files; the send button is disabled meanwhile', async () => {
      const pending = new Subject<CommentCreated>();
      api.addComment.and.returnValue(pending);
      const element = await open();
      const file = fakeFile('צילום.png');
      await pick(element, file);
      type(element, '  הנה הצילום  ');
      send(element);

      expect(api.addComment).toHaveBeenCalledOnceWith(1042, 'הנה הצילום', [file]);
      expect(api.addAttachments).not.toHaveBeenCalled();
      const button = element.querySelector<HTMLButtonElement>('app-ticket-reply button[type="submit"]')!;
      expect(button.disabled).toBeTrue();
      expect(button.textContent).toContain('שולח...');
    });

    it('files without text: sent as attachments', async () => {
      api.addAttachments.and.returnValue(of([fakeAttachment()]));
      const element = await open();
      const file = fakeFile('צילום.png');
      await pick(element, file);
      send(element);
      expect(api.addAttachments).toHaveBeenCalledOnceWith(1042, [file]);
      expect(api.addComment).not.toHaveBeenCalled();
    });

    it('after success: the timeline is reloaded (the reply appears), the fields reset, the focus is back on the text', async () => {
      api.addComment.and.returnValue(of({ id: 5 }));
      const element = await open();
      api.history.and.returnValue(of(fakeHistory([fakeComment({ id: 5, isFromRequester: true, body: 'הנה הצילום' })])));
      await pick(element, fakeFile('צילום.png'));
      type(element, 'הנה הצילום');
      send(element);
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(api.history).toHaveBeenCalledTimes(2);
      expect(element.querySelector('app-timeline .from-me')!.textContent).toContain('הנה הצילום');
      const textarea = element.querySelector<HTMLTextAreaElement>('app-ticket-reply textarea')!;
      expect(textarea.value).toBe('');
      expect(element.querySelectorAll('app-ticket-reply .file').length).toBe(0);
      expect(document.activeElement).toBe(textarea);
      expect(notifications.success).toHaveBeenCalledWith('התגובה נשלחה');
    });

    it('400 on the body: next to the field, the text is kept', async () => {
      api.addComment.and.returnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 400,
              error: { title: 'הבקשה אינה תקינה', status: 400, errors: { body: ['התגובה: עד 4,000 תווים'] } },
            }),
        ),
      );
      const element = await open();
      type(element, 'טקסט');
      send(element);
      expect(element.querySelector('app-ticket-reply mat-error')!.textContent).toContain('התגובה: עד 4,000 תווים');
      expect(element.querySelector<HTMLTextAreaElement>('app-ticket-reply textarea')!.value).toBe('טקסט');
    });

    it('400 on a file: next to the file picker', async () => {
      api.addAttachments.and.returnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 400,
              error: { title: 'הבקשה אינה תקינה', status: 400, errors: { 'files[0]': ['תוכן הקובץ אינו תואם לסוג הקובץ'] } },
            }),
        ),
      );
      const element = await open();
      await pick(element, fakeFile('מזויף.pdf'));
      send(element);
      expect(element.querySelector('app-ticket-reply app-file-upload .messages')!.textContent).toContain(
        'מזויף.pdf: תוכן הקובץ אינו תואם לסוג הקובץ',
      );
      expect(element.querySelector('app-ticket-reply app-form-alert')).toBeNull();
    });
  });

  describe('closed tickets', () => {
    for (const status of ['Closed', 'Cancelled'] as const) {
      it(`${status}: a note instead of the reply`, async () => {
        api.get.and.returnValue(of(fakeTicketDetails({ status })));
        const element = await open();
        expect(element.querySelector('app-ticket-reply')).toBeNull();
        expect(element.querySelector('.closed-note')!.textContent).toContain(CLOSED_NOTE);
      });
    }

    it('422 ticket-closed from the server: the same note', async () => {
      api.addComment.and.returnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 422,
              error: { type: 'https://errors.example/ticket-closed', title: 'הפנייה סגורה', status: 422 },
            }),
        ),
      );
      const element = await open();
      api.get.and.returnValue(of(fakeTicketDetails({ status: 'InProgress' })));
      type(element, 'עוד משהו');
      send(element);
      harness.detectChanges();

      expect(element.querySelector('app-ticket-reply')).toBeNull();
      expect(element.querySelector('.closed-note')!.textContent).toContain(CLOSED_NOTE);
    });
  });

  it('WaitingForEmployee: a banner above the reply', async () => {
    api.get.and.returnValue(of(fakeTicketDetails({ status: 'WaitingForEmployee' })));
    const element = await open();
    const banner = element.querySelector('app-ticket-reply .waiting-banner')!;
    expect(banner.textContent).toContain('המטפל ביקש ממך מידע נוסף. יש להשיב כאן.');
    expect(banner.getAttribute('role')).toBe('status');
  });

  it('no banner in another status', async () => {
    const element = await open();
    expect(element.querySelector('.waiting-banner')).toBeNull();
  });

  it('after replying to WaitingForEmployee the ticket is reloaded and the banner is gone', async () => {
    api.get.and.returnValue(of(fakeTicketDetails({ status: 'WaitingForEmployee' })));
    api.addComment.and.returnValue(of({ id: 9 }));
    const element = await open();
    expect(element.querySelector('.waiting-banner')).not.toBeNull();

    // The server moved it back to handling (an automatic transition).
    api.get.and.returnValue(of(fakeTicketDetails({ status: 'InProgress' })));
    type(element, 'מספר המדפסת: 2-14');
    send(element);
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(api.get).toHaveBeenCalledTimes(2);
    expect(element.querySelector('.waiting-banner')).toBeNull();
    expect(element.querySelector('p[aria-live]')!.textContent).toContain('סטטוס הפנייה עודכן: בטיפול');
  });

  it('the reply is shown by allowedActions, not by the status', async () => {
    api.get.and.returnValue(of(fakeTicketDetails({ status: 'InProgress', allowedActions: [] })));
    const element = await open();
    expect(element.querySelector('app-ticket-reply')).toBeNull();
    expect(element.querySelector('.closed-note')).not.toBeNull();
  });

  // ---- status actions (task 3.5) ----

  const UPDATED = () => of({ id: 1042, rowVersion: 'new', canView: true });

  function actionButtons(element: HTMLElement): string[] {
    return Array.from(element.querySelectorAll('[data-action]')).map((button) => button.getAttribute('data-action')!);
  }

  function click(element: HTMLElement, action: string): void {
    element.querySelector<HTMLButtonElement>(`[data-action="${action}"]`)!.click();
    harness.detectChanges();
  }

  describe('cancel', () => {
    it('shown when the server allows it', async () => {
      api.get.and.returnValue(of(fakeTicketDetails({ status: 'Assigned', allowedActions: ['reply', 'cancel'] })));
      const element = await open();
      expect(actionButtons(element)).toEqual(['cancel']);
      expect(element.querySelector('[data-action="cancel"]')!.textContent).toContain('ביטול פנייה');
    });

    it('not shown when the server does not allow it', async () => {
      api.get.and.returnValue(of(fakeTicketDetails({ status: 'InProgress', allowedActions: ['reply'] })));
      const element = await open();
      expect(actionButtons(element)).toEqual([]);
    });

    it('asks first (it cannot be undone), then cancels with the loaded version and reloads', async () => {
      api.get.and.returnValue(of(fakeTicketDetails({ allowedActions: ['reply', 'cancel'] })));
      api.changeStatus.and.returnValue(UPDATED());
      confirm.confirm.and.returnValue(of(true));
      const element = await open();
      api.get.and.returnValue(of(fakeTicketDetails({ status: 'Cancelled' })));

      click(element, 'cancel');
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(confirm.confirm).toHaveBeenCalledWith(
        jasmine.objectContaining({ tone: 'danger', message: jasmine.stringContaining('לא ניתן לבטל את הביטול') }),
      );
      expect(api.changeStatus).toHaveBeenCalledWith(1042, { newStatus: 'Cancelled', rowVersion: 'AAAAAAAAA+g=' });
      expect(api.get).toHaveBeenCalledTimes(2);
      expect(notifications.success).toHaveBeenCalledWith('הפנייה בוטלה');
      expect(actionButtons(element)).toEqual([]);
      expect(element.querySelector('.closed-note')).not.toBeNull();
    });

    it('not confirmed: nothing is sent', async () => {
      api.get.and.returnValue(of(fakeTicketDetails({ allowedActions: ['reply', 'cancel'] })));
      confirm.confirm.and.returnValue(of(false));
      const element = await open();
      click(element, 'cancel');

      expect(api.changeStatus).not.toHaveBeenCalled();
    });

    it('409: a message and the ticket is reloaded', async () => {
      api.get.and.returnValue(of(fakeTicketDetails({ allowedActions: ['reply', 'cancel'] })));
      api.changeStatus.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
      confirm.confirm.and.returnValue(of(true));
      const element = await open();
      click(element, 'cancel');
      await harness.fixture.whenStable();

      expect(notifications.error).toHaveBeenCalledWith(jasmine.stringContaining('הפנייה עודכנה בינתיים'));
      expect(api.get).toHaveBeenCalledTimes(2);
    });
  });

  describe('resolved', () => {
    const resolved = () =>
      fakeTicketDetails({
        status: 'Resolved',
        // 00:30 on 11/11 in Israel – the window ends on 18/11 Israel time (17/11 in UTC).
        resolvedAt: '2026-11-10T22:30:00Z',
        allowedActions: ['reply', 'confirmResolved', 'reopen'],
      });
    const resolutionHistory = () =>
      fakeHistory([
        { kind: 'status', at: '2026-11-10T08:00:00Z', previousStatus: null, newStatus: 'New', changedByName: 'ישראל ישראלי' },
        fakeComment({ id: 3, body: 'הוחלף ספק הכוח.\nהמחשב עובד.', isResolution: true, at: '2026-11-10T22:30:00Z' }),
      ]);

    beforeEach(() => {
      api.get.and.returnValue(of(resolved()));
      api.history.and.returnValue(of(resolutionHistory()));
    });

    it('a prominent area with the resolution, both answers and the reopen window', async () => {
      const element = await open();
      const area = element.querySelector('.resolved-card')!;

      expect(area.textContent).toContain('המטפל סימן את הפנייה כנפתרה');
      expect(area.querySelector('.resolution-body')!.textContent).toBe('הוחלף ספק הכוח.\nהמחשב עובד.');
      expect(actionButtons(element)).toEqual(['confirmResolved', 'reopen']);
      expect(area.querySelector('[data-action="confirmResolved"]')!.textContent).toContain('הבעיה נפתרה');
      expect(area.querySelector('[data-action="reopen"]')!.textContent).toContain('הבעיה לא נפתרה – פתיחה מחדש');
      expect(area.querySelector('[data-test="reopen-window"]')!.textContent).toContain(
        'ניתן לפתוח מחדש עד 18/11/2026. אחרי תאריך זה הפנייה תיסגר אוטומטית.',
      );
    });

    it('without the reopen action (the window passed): no reopen button and no window text', async () => {
      api.get.and.returnValue(of({ ...resolved(), allowedActions: ['reply', 'confirmResolved'] }));
      const element = await open();
      expect(actionButtons(element)).toEqual(['confirmResolved']);
      expect(element.querySelector('[data-test="reopen-window"]')).toBeNull();
    });

    it('no area in another status', async () => {
      api.get.and.returnValue(of(fakeTicketDetails({ status: 'InProgress' })));
      const element = await open();
      expect(element.querySelector('.resolved-card')).toBeNull();
    });

    it('"הבעיה נפתרה" asks first, then closes the ticket and reloads', async () => {
      api.changeStatus.and.returnValue(UPDATED());
      confirm.confirm.and.returnValue(of(true));
      const element = await open();
      api.get.and.returnValue(of(fakeTicketDetails({ status: 'Closed' })));

      click(element, 'confirmResolved');
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(confirm.confirm).toHaveBeenCalledWith(jasmine.objectContaining({ message: jasmine.stringContaining('תיסגר סופית') }));
      expect(api.changeStatus).toHaveBeenCalledWith(1042, { newStatus: 'Closed', rowVersion: 'AAAAAAAAA+g=' });
      expect(notifications.success).toHaveBeenCalledWith('הפנייה נסגרה. תודה!');
      expect(element.querySelector('.resolved-card')).toBeNull();
    });

    it('reopen: a dialog with an optional message, sent with the change', async () => {
      api.changeStatus.and.returnValue(UPDATED());
      const element = await open();
      click(element, 'reopen');
      await harness.fixture.whenStable();

      const dialog = document.querySelector<HTMLElement>('app-reopen-dialog')!;
      expect(dialog.textContent).toContain('פתיחת הפנייה מחדש');
      const field = dialog.querySelector<HTMLTextAreaElement>('textarea')!;
      field.value = '  עדיין לא נדלק  ';
      field.dispatchEvent(new Event('input'));
      harness.detectChanges();
      dialog.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(api.changeStatus).toHaveBeenCalledWith(1042, {
        newStatus: 'InProgress',
        rowVersion: 'AAAAAAAAA+g=',
        message: 'עדיין לא נדלק',
      });
      expect(notifications.success).toHaveBeenCalledWith('הפנייה נפתחה מחדש');
    });

    it('reopen without a message: no message is sent', async () => {
      api.changeStatus.and.returnValue(UPDATED());
      const element = await open();
      click(element, 'reopen');
      await harness.fixture.whenStable();
      document.querySelector<HTMLButtonElement>('app-reopen-dialog button[type="submit"]')!.click();
      harness.detectChanges();

      expect(api.changeStatus).toHaveBeenCalledWith(1042, { newStatus: 'InProgress', rowVersion: 'AAAAAAAAA+g=' });
    });

    it('reopen 409: the dialog stays with the text and a message', async () => {
      api.changeStatus.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
      const element = await open();
      click(element, 'reopen');
      await harness.fixture.whenStable();
      const dialog = document.querySelector<HTMLElement>('app-reopen-dialog')!;
      const field = dialog.querySelector<HTMLTextAreaElement>('textarea')!;
      field.value = 'לא עובד';
      field.dispatchEvent(new Event('input'));
      dialog.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
      harness.detectChanges();
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(document.querySelector('app-reopen-dialog')!.textContent).toContain('הפנייה עודכנה בינתיים');
      expect(field.value).toBe('לא עובד');
      expect(api.get).toHaveBeenCalledTimes(2);
    });
  });

  afterEach(() => {
    // Dialogs live in the overlay container, outside the fixture.
    document.querySelectorAll('.cdk-overlay-container').forEach((container) => (container.innerHTML = ''));
  });

  describe('leaving the screen', () => {
    const router = () => TestBed.inject(Router);

    it('files chosen without text: asks to confirm', async () => {
      confirm.confirm.and.returnValue(of(false));
      const element = await open();
      await pick(element, fakeFile('צילום.png'));
      const left = await router().navigateByUrl('/employee');
      expect(left).toBeFalse();
      expect(confirm.confirm).toHaveBeenCalledOnceWith(jasmine.objectContaining({ message: 'הקבצים שבחרת לא נשלחו.' }));
    });

    it('text typed: asks to confirm', async () => {
      confirm.confirm.and.returnValue(of(true));
      const element = await open();
      type(element, 'התחלתי לכתוב');
      const left = await router().navigateByUrl('/employee');
      expect(left).toBeTrue();
      expect(confirm.confirm).toHaveBeenCalledOnceWith(
        jasmine.objectContaining({ message: 'הטקסט שהזנת לא נשמר ויימחק.' }),
      );
    });

    it('nothing typed: leaves without asking', async () => {
      await open();
      expect(await router().navigateByUrl('/employee')).toBeTrue();
      expect(confirm.confirm).not.toHaveBeenCalled();
    });
  });
});
