import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { EMPTY, of, throwError } from 'rxjs';

import { unsavedChangesGuard } from '../../../guards/unsaved-changes.guard';
import { TicketAction } from '../../../models/ticket.models';
import { AttachmentDownloadService } from '../../../services/attachment-download.service';
import { AuthService } from '../../../services/auth.service';
import { CategoriesApiService } from '../../../services/categories-api.service';
import { ConfirmService } from '../../../services/confirm.service';
import { NotificationService } from '../../../services/notification.service';
import { TicketsApiService } from '../../../services/tickets-api.service';
import { fakeTicketDetails } from '../../employee/testing/ticket-fixtures';
import { CONFLICT_MESSAGE, LEFT_SCOPE_MESSAGE } from '../handler-messages';
import { HANDLER_ID, fakeAuth, fakeStaffDetails, fakeStaffHistory } from '../testing/handler-fixtures';
import { TicketHandlePage } from './ticket-handle-page';

@Component({ template: '' })
class Stub {}

const CONFLICT = () => throwError(() => new HttpErrorResponse({ status: 409 }));

describe('TicketHandlePage', () => {
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
      'reclassify',
      'assign',
      'assignOptions',
      'changeStatus',
    ]);
    api.get.and.returnValue(of(fakeStaffDetails()));
    api.history.and.returnValue(of(fakeStaffHistory()));
    api.addComment.and.returnValue(of({ id: 1 }));
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'error']);
    confirm = jasmine.createSpyObj<ConfirmService>('ConfirmService', ['confirm']);
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'handler', pathMatch: 'full', component: Stub },
            { path: 'handler/assigned', component: Stub },
            { path: 'employee/tickets/:id', component: Stub },
            { path: 'handler/tickets/:id', component: TicketHandlePage, canDeactivate: [unsavedChangesGuard] },
          ],
          withComponentInputBinding(),
        ),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: TicketsApiService, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: ConfirmService, useValue: confirm },
        { provide: AuthService, useValue: fakeAuth() },
        { provide: AttachmentDownloadService, useValue: { download: () => EMPTY } },
        {
          provide: CategoriesApiService,
          useValue: { list: () => of([{ id: 3, name: 'מחשוב' }, { id: 4, name: 'שכר' }]) },
        },
      ],
    });
  });

  afterEach(() => {
    // Dialogs live in the overlay container, outside the fixture.
    document.querySelectorAll('.cdk-overlay-container').forEach((container) => (container.innerHTML = ''));
  });

  async function open(actions?: TicketAction[]): Promise<HTMLElement> {
    if (actions) {
      api.get.and.returnValue(of(fakeStaffDetails({ allowedActions: actions })));
    }
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/handler/tickets/152', TicketHandlePage);
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  function type(field: HTMLTextAreaElement, value: string): void {
    field.value = value;
    field.dispatchEvent(new Event('input'));
    harness.detectChanges();
  }

  function composerText(element: HTMLElement): HTMLTextAreaElement {
    return element.querySelector<HTMLTextAreaElement>('app-ticket-composer textarea')!;
  }

  function submitComposer(element: HTMLElement): void {
    element.querySelector<HTMLButtonElement>('app-ticket-composer button[type="submit"]')!.click();
    harness.detectChanges();
  }

  function modeButton(element: HTMLElement, mode: 'internal' | 'reply'): HTMLButtonElement {
    return element.querySelector<HTMLButtonElement>(`mat-button-toggle[data-mode="${mode}"] button`)!;
  }

  function actionButtons(element: HTMLElement): string[] {
    return Array.from(element.querySelectorAll('[data-action]')).map((button) => button.getAttribute('data-action')!);
  }

  async function settle(): Promise<void> {
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  // ---- area 1, 2, 5 ----

  it('shows the ticket, the employee (name, department, role), the AI placeholder and the staff timeline', async () => {
    api.history.and.returnValue(
      of(
        fakeStaffHistory([
          { kind: 'status', at: '2026-11-16T08:00:00Z', previousStatus: null, newStatus: 'New', changedByName: 'נועה', reason: null },
          {
            kind: 'comment',
            at: '2026-11-16T08:10:00Z',
            id: 5,
            authorName: 'אלון ביטון',
            isFromRequester: false,
            body: 'בדקתי – בעיית דרייבר',
            isInternal: true,
            isResolution: false,
            attachments: [],
          },
        ]),
      ),
    );
    const element = await open();

    expect(element.querySelector('h1')!.textContent).toContain('המדפסת לא מדפיסה');
    const details = element.querySelector('.requester')!.textContent!;
    expect(details).toContain('נועה ברקוביץ');
    expect(details).toContain('פיתוח');
    expect(details).toContain('מפתחת');
    expect(element.textContent).toContain('ניתוח AI יתווסף בהמשך');
    const note = element.querySelector('app-timeline li.internal')!;
    expect(note.textContent).toContain('פנימי');
    expect(note.textContent).toContain('בעיית דרייבר');
  });

  it('a ticket the user opened (the requester view) opens on the employee screen', async () => {
    api.get.and.returnValue(of(fakeTicketDetails({ id: 152 })));
    await open();
    await settle();

    expect(TestBed.inject(Router).url).toBe('/employee/tickets/152');
  });

  // ---- area 4: actions by allowedActions ----

  it('shows only the actions the server allows: a handler with a personal ticket', async () => {
    const element = await open(['reply', 'internalNote', 'reclassify', 'assign', 'transferToDepartment']);
    expect(actionButtons(element)).toEqual(['reclassify', 'assign']);
  });

  it('shows only the actions the server allows: a ticket in the queue', async () => {
    const element = await open(['internalNote', 'reclassify', 'assign', 'takeFromQueue']);
    expect(actionButtons(element)).toEqual(['takeFromQueue', 'reclassify', 'assign']);
  });

  it('shows only the actions the server allows: none (view only)', async () => {
    const element = await open([]);
    expect(actionButtons(element)).toEqual([]);
    expect(element.textContent).toContain('אין פעולות זמינות');
    expect(element.querySelector('app-ticket-composer')).toBeNull();
  });

  it('"קח לטיפולי" assigns the ticket to the user, with the loaded version', async () => {
    const element = await open(['internalNote', 'assign', 'takeFromQueue']);
    api.assign.and.returnValue(of({ id: 152, rowVersion: 'new', canView: true }));
    element.querySelector<HTMLButtonElement>('[data-action="takeFromQueue"]')!.click();
    harness.detectChanges();

    expect(api.assign).toHaveBeenCalledWith(152, { targetUserId: HANDLER_ID, rowVersion: 'AAAAAAAAB9E=' });
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  // ---- area 3: writing ----

  it('the default is an internal note: yellow, "גלוי לצוות בלבד", sent with isInternal=true', async () => {
    const element = await open();
    const composer = element.querySelector('app-ticket-composer .composer')!;
    expect(composer.classList).toContain('mode-internal');
    expect(composer.textContent).toContain('גלוי לצוות בלבד');
    expect(element.querySelector('app-ticket-composer button[type="submit"]')!.textContent).toContain('שמירת הערה פנימית');

    type(composerText(element), 'הערה לצוות');
    submitComposer(element);

    expect(api.addComment).toHaveBeenCalledWith(152, 'הערה לצוות', [], true);
  });

  it('a reply to the employee is sent with isInternal=false', async () => {
    const element = await open();
    modeButton(element, 'reply').click();
    harness.detectChanges();
    expect(element.querySelector('app-ticket-composer .composer')!.classList).toContain('mode-reply');
    expect(element.querySelector('app-ticket-composer button[type="submit"]')!.textContent).toContain('שליחה לעובד');

    type(composerText(element), 'שלום, טיפלתי בבעיה');
    submitComposer(element);

    expect(api.addComment).toHaveBeenCalledWith(152, 'שלום, טיפלתי בבעיה', [], false);
  });

  it('switching the mode keeps the text, and the mode is announced', async () => {
    const element = await open();
    type(composerText(element), 'טקסט שנכתב');

    modeButton(element, 'reply').click();
    harness.detectChanges();
    expect(composerText(element).value).toBe('טקסט שנכתב');
    expect(element.querySelector('app-ticket-composer [aria-live]')!.textContent).toContain('תגובה לעובד');

    modeButton(element, 'internal').click();
    harness.detectChanges();
    expect(composerText(element).value).toBe('טקסט שנכתב');
  });

  it('only the allowed modes: a department manager writes internal notes only', async () => {
    const element = await open(['internalNote', 'reclassify', 'assign']);
    expect(element.querySelector('mat-button-toggle-group')).toBeNull();
    expect(element.querySelector('app-ticket-composer .composer')!.classList).toContain('mode-internal');
  });

  it('only a reply allowed: the reply mode, without a switch', async () => {
    const element = await open(['reply']);
    expect(element.querySelector('mat-button-toggle-group')).toBeNull();
    expect(element.querySelector('app-ticket-composer .composer')!.classList).toContain('mode-reply');
  });

  it('an empty internal note is not sent', async () => {
    const element = await open();
    submitComposer(element);
    expect(api.addComment).not.toHaveBeenCalled();
    expect(element.querySelector('app-ticket-composer mat-error')!.textContent).toContain('יש לכתוב את ההערה הפנימית');
  });

  it('409 while sending: the message of 11.2, the ticket is reloaded, and the text stays', async () => {
    const element = await open();
    api.addComment.and.returnValue(CONFLICT());
    type(composerText(element), 'טקסט חשוב');
    submitComposer(element);
    await settle();

    expect(element.querySelector('app-ticket-composer app-form-alert')!.textContent).toContain(CONFLICT_MESSAGE);
    expect(composerText(element).value).toBe('טקסט חשוב');
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it('leaving with text in the writing area asks first', async () => {
    const element = await open();
    type(composerText(element), 'טיוטה');
    confirm.confirm.and.returnValue(of(false));

    await harness.navigateByUrl('/handler/assigned');

    expect(confirm.confirm).toHaveBeenCalled();
    expect(TestBed.inject(Router).url).toBe('/handler/tickets/152');
  });

  // ---- dialogs ----

  function dialog(selector: string): HTMLElement {
    return document.querySelector<HTMLElement>(selector)!;
  }

  function dialogReason(selector: string): HTMLTextAreaElement {
    return dialog(selector).querySelector<HTMLTextAreaElement>('textarea[formControlName="reason"]')!;
  }

  function submitDialog(selector: string): void {
    dialog(selector).querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    harness.detectChanges();
  }

  async function openDialog(element: HTMLElement, action: 'reclassify' | 'assign'): Promise<void> {
    element.querySelector<HTMLButtonElement>(`[data-action="${action}"]`)!.click();
    await settle();
  }

  it('reclassification requires a reason', async () => {
    const element = await open();
    await openDialog(element, 'reclassify');

    submitDialog('app-reclassify-dialog');

    expect(api.reclassify).not.toHaveBeenCalled();
    expect(dialog('app-reclassify-dialog').textContent).toContain('יש לכתוב נימוק');
  });

  it('reclassification sends the category, urgency and reason with the loaded version', async () => {
    const element = await open();
    await openDialog(element, 'reclassify');
    api.reclassify.and.returnValue(of({ id: 152, rowVersion: 'new', canView: true }));

    type(dialogReason('app-reclassify-dialog'), 'הקטגוריה שגויה');
    submitDialog('app-reclassify-dialog');
    await settle();

    expect(api.reclassify).toHaveBeenCalledWith(152, {
      categoryId: 3,
      urgency: 'High',
      reason: 'הקטגוריה שגויה',
      rowVersion: 'AAAAAAAAB9E=',
    });
    expect(document.querySelector('app-reclassify-dialog')).toBeNull();
  });

  it('409 in the reclassification dialog: the message, the ticket reloaded, the reason kept; the retry sends the new version', async () => {
    const element = await open();
    await openDialog(element, 'reclassify');
    api.reclassify.and.returnValue(CONFLICT());
    api.get.and.returnValue(of(fakeStaffDetails({ rowVersion: 'AAAAAAAAB9F=' })));

    type(dialogReason('app-reclassify-dialog'), 'נימוק ארוך');
    submitDialog('app-reclassify-dialog');
    await settle();

    expect(dialog('app-reclassify-dialog').textContent).toContain(CONFLICT_MESSAGE);
    expect(dialogReason('app-reclassify-dialog').value).toBe('נימוק ארוך');
    expect(api.get).toHaveBeenCalledTimes(2);

    api.reclassify.and.returnValue(of({ id: 152, rowVersion: 'x', canView: true }));
    submitDialog('app-reclassify-dialog');
    expect(api.reclassify.calls.mostRecent().args[1].rowVersion).toBe('AAAAAAAAB9F=');
  });

  it('a change that moved the ticket out of the user\'s scope: a message and back to the queue', async () => {
    const element = await open();
    await openDialog(element, 'reclassify');
    api.reclassify.and.returnValue(of({ id: 152, rowVersion: 'new', canView: false }));

    type(dialogReason('app-reclassify-dialog'), 'שייך למחלקה אחרת');
    submitDialog('app-reclassify-dialog');
    await settle();

    expect(notifications.success).toHaveBeenCalledWith(LEFT_SCOPE_MESSAGE);
    expect(TestBed.inject(Router).url).toBe('/handler');
  });

  it('the transfer dialog of a manager: handlers only, and a reason is required', async () => {
    api.assignOptions.and.returnValue(of({ handlers: [{ id: 9, name: 'דנה לוי', openTicketCount: 2 }], departments: [] }));
    const element = await open(['internalNote', 'assign']);
    await openDialog(element, 'assign');

    expect(dialog('app-assign-dialog').querySelector('mat-radio-group')).toBeNull();
    submitDialog('app-assign-dialog');
    expect(api.assign).not.toHaveBeenCalled();
    expect(dialog('app-assign-dialog').textContent).toContain('יש לכתוב נימוק');
  });

  it('the transfer dialog of a handler also offers another department', async () => {
    api.assignOptions.and.returnValue(
      of({ handlers: [{ id: 9, name: 'דנה לוי', openTicketCount: 2 }], departments: [{ id: 11, name: 'משאבי אנוש' }] }),
    );
    const element = await open(['reply', 'internalNote', 'assign', 'transferToDepartment']);
    await openDialog(element, 'assign');

    expect(dialog('app-assign-dialog').querySelector('mat-radio-group')).not.toBeNull();
    expect(dialog('app-assign-dialog').textContent).toContain('מחלקה אחרת');
  });

  it('409 in the transfer dialog keeps the reason and reloads the ticket', async () => {
    api.assignOptions.and.returnValue(of({ handlers: [{ id: 9, name: 'דנה לוי', openTicketCount: 2 }], departments: [] }));
    const element = await open();
    await openDialog(element, 'assign');
    api.assign.and.returnValue(CONFLICT());

    // Choose the handler (mat-select: open and pick the option).
    dialog('app-assign-dialog').querySelector<HTMLElement>('mat-select[formControlName="userId"] .mat-mdc-select-trigger')!.click();
    await settle();
    document.querySelector<HTMLElement>('mat-option')!.click();
    await settle();
    type(dialogReason('app-assign-dialog'), 'עומס במחלקה');
    submitDialog('app-assign-dialog');
    await settle();

    expect(api.assign).toHaveBeenCalledWith(152, { targetUserId: 9, reason: 'עומס במחלקה', rowVersion: 'AAAAAAAAB9E=' });
    expect(dialog('app-assign-dialog').textContent).toContain(CONFLICT_MESSAGE);
    expect(dialogReason('app-assign-dialog').value).toBe('עומס במחלקה');
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  // ---- status actions (task 3.5) ----

  const UPDATED = () => of({ id: 152, rowVersion: 'new', canView: true });
  const IN_PROGRESS_ACTIONS: TicketAction[] = ['reply', 'internalNote', 'reclassify', 'assign', 'requestInfo', 'resolve'];

  function sendButtons(element: HTMLElement): string[] {
    return Array.from(element.querySelectorAll('app-ticket-composer [data-send]')).map(
      (button) => button.getAttribute('data-send')!,
    );
  }

  function sendButton(element: HTMLElement, kind: 'comment' | 'requestInfo' | 'resolve'): HTMLButtonElement {
    return element.querySelector<HTMLButtonElement>(`app-ticket-composer [data-send="${kind}"]`)!;
  }

  async function openInReplyMode(actions: TicketAction[] = IN_PROGRESS_ACTIONS): Promise<HTMLElement> {
    const element = await open(actions);
    modeButton(element, 'reply').click();
    harness.detectChanges();
    return element;
  }

  it('"התחל טיפול" is shown when allowed, first and as the main button', async () => {
    const element = await open(['reply', 'internalNote', 'reclassify', 'assign', 'start']);
    expect(actionButtons(element)).toEqual(['start', 'reclassify', 'assign']);
    const start = element.querySelector('[data-action="start"]')!;
    expect(start.classList).toContain('btn-primary-gradient');
    expect(start.textContent).toContain('התחל טיפול');
  });

  it('in progress: no "התחל טיפול"; the request-info and resolution shortcuts instead', async () => {
    const element = await open(IN_PROGRESS_ACTIONS);
    expect(actionButtons(element)).toEqual(['reclassify', 'assign', 'requestInfo', 'resolve']);
  });

  it('"התחל טיפול" changes the status to InProgress with the loaded version, and reloads', async () => {
    const element = await open(['reply', 'internalNote', 'start']);
    api.changeStatus.and.returnValue(UPDATED());
    api.get.and.returnValue(of(fakeStaffDetails({ allowedActions: IN_PROGRESS_ACTIONS })));
    element.querySelector<HTMLButtonElement>('[data-action="start"]')!.click();
    await settle();

    expect(api.changeStatus).toHaveBeenCalledWith(152, { newStatus: 'InProgress', rowVersion: 'AAAAAAAAB9E=' });
    expect(api.get).toHaveBeenCalledTimes(2);
    expect(api.history).toHaveBeenCalledTimes(2);
    expect(actionButtons(element)).not.toContain('start');
    expect(notifications.success).toHaveBeenCalledWith('הטיפול בפנייה התחיל');
  });

  it('"התחל טיפול" with 409: the message of 11.2 and the ticket is reloaded', async () => {
    const element = await open(['reply', 'internalNote', 'start']);
    api.changeStatus.and.returnValue(CONFLICT());
    element.querySelector<HTMLButtonElement>('[data-action="start"]')!.click();
    await settle();

    expect(notifications.error).toHaveBeenCalledWith(CONFLICT_MESSAGE);
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it('the status change is announced to screen readers', async () => {
    api.get.and.returnValue(of(fakeStaffDetails({ status: 'Assigned', allowedActions: ['reply', 'internalNote', 'start'] })));
    const element = await open();
    expect(element.querySelector('p[aria-live]')!.textContent!.trim()).toBe('');
    api.changeStatus.and.returnValue(UPDATED());
    api.get.and.returnValue(of(fakeStaffDetails({ status: 'InProgress', allowedActions: IN_PROGRESS_ACTIONS })));
    element.querySelector<HTMLButtonElement>('[data-action="start"]')!.click();
    await settle();

    expect(element.querySelector('p[aria-live]')!.textContent).toContain('סטטוס הפנייה עודכן: בטיפול');
  });

  it('the internal mode only saves a note; the reply mode adds the status sends', async () => {
    const element = await open(IN_PROGRESS_ACTIONS);
    expect(sendButtons(element)).toEqual(['comment']);
    expect(sendButton(element, 'comment').textContent).toContain('שמירת הערה פנימית');

    modeButton(element, 'reply').click();
    harness.detectChanges();
    expect(sendButtons(element)).toEqual(['resolve', 'requestInfo', 'comment']);
    expect(sendButton(element, 'comment').classList).toContain('btn-primary-gradient');
    expect(sendButton(element, 'resolve').textContent).toContain('שליחה כפתרון');
    expect(sendButton(element, 'requestInfo').textContent).toContain('שליחה ובקשת מידע');
  });

  it('the status sends are shown only when allowed: resolve only', async () => {
    const element = await openInReplyMode(['reply', 'internalNote', 'resolve']);
    expect(sendButtons(element)).toEqual(['resolve', 'comment']);
  });

  it('the status sends are shown only when allowed: none (waiting for the employee)', async () => {
    const element = await openInReplyMode(['reply', 'internalNote']);
    expect(sendButtons(element)).toEqual(['comment']);
  });

  it('"שליחה לעובד" sends a visible comment, without a status change', async () => {
    const element = await openInReplyMode();
    type(composerText(element), 'עדכון קצר');
    sendButton(element, 'comment').click();
    await settle();

    expect(api.addComment).toHaveBeenCalledWith(152, 'עדכון קצר', [], false);
    expect(api.changeStatus).not.toHaveBeenCalled();
  });

  it('"שליחה ובקשת מידע" changes the status to WaitingForEmployee with the text as the message', async () => {
    const element = await openInReplyMode();
    api.changeStatus.and.returnValue(UPDATED());
    type(composerText(element), 'מה מספר המדפסת?');
    sendButton(element, 'requestInfo').click();
    await settle();

    expect(api.changeStatus).toHaveBeenCalledWith(152, {
      newStatus: 'WaitingForEmployee',
      rowVersion: 'AAAAAAAAB9E=',
      message: 'מה מספר המדפסת?',
    });
    expect(api.addComment).not.toHaveBeenCalled();
    expect(confirm.confirm).not.toHaveBeenCalled();
    expect(notifications.success).toHaveBeenCalledWith('הבקשה למידע נשלחה לעובד');
  });

  it('"שליחה כפתרון" asks first, then changes the status to Resolved with the text; the files follow', async () => {
    const element = await openInReplyMode();
    api.changeStatus.and.returnValue(UPDATED());
    api.addAttachments.and.returnValue(of([]));
    confirm.confirm.and.returnValue(of(true));
    type(composerText(element), 'הוחלף הדרייבר');
    const file = new File([new Uint8Array(10)], 'log.png');
    // Set the files through the form (the file input is covered by FileUpload's own tests).
    (harness.fixture.debugElement.query((de) => de.name === 'app-ticket-composer').componentInstance as {
      form: { controls: { files: { setValue(files: File[]): void } } };
    }).form.controls.files.setValue([file]);
    harness.detectChanges();
    sendButton(element, 'resolve').click();
    await settle();

    expect(confirm.confirm).toHaveBeenCalledWith(
      jasmine.objectContaining({ message: 'הפנייה תסומן כנפתרה. העובד יוכל לאשר או לפתוח מחדש תוך 7 ימים.' }),
    );
    expect(api.changeStatus).toHaveBeenCalledWith(152, {
      newStatus: 'Resolved',
      rowVersion: 'AAAAAAAAB9E=',
      message: 'הוחלף הדרייבר',
    });
    expect(api.addAttachments).toHaveBeenCalledWith(152, [file]);
    expect(notifications.success).toHaveBeenCalledWith('הפתרון נשלח לעובד');
  });

  it('"שליחה כפתרון" cancelled in the confirmation: nothing is sent and the text stays', async () => {
    const element = await openInReplyMode();
    confirm.confirm.and.returnValue(of(false));
    type(composerText(element), 'פתרון');
    sendButton(element, 'resolve').click();
    await settle();

    expect(api.changeStatus).not.toHaveBeenCalled();
    expect(composerText(element).value).toBe('פתרון');
  });

  it('after a status send: the ticket, the timeline and the actions are reloaded, and the field is cleared', async () => {
    const element = await openInReplyMode();
    api.changeStatus.and.returnValue(UPDATED());
    api.get.and.returnValue(of(fakeStaffDetails({ status: 'WaitingForEmployee', allowedActions: ['reply', 'internalNote'] })));
    type(composerText(element), 'נא לצרף צילום מסך');
    sendButton(element, 'requestInfo').click();
    await settle();

    expect(api.get).toHaveBeenCalledTimes(2);
    expect(api.history).toHaveBeenCalledTimes(2);
    expect(composerText(element).value).toBe('');
    expect(actionButtons(element)).not.toContain('requestInfo');
    expect(sendButtons(element)).toEqual(['comment']);
  });

  it('a status send needs text: files alone are not sent', async () => {
    const element = await openInReplyMode();
    sendButton(element, 'requestInfo').click();
    harness.detectChanges();

    expect(api.changeStatus).not.toHaveBeenCalled();
    expect(element.querySelector('app-ticket-composer mat-error')!.textContent).toContain('יש לכתוב לעובד איזה מידע חסר');
  });

  it('409 on a status send: the message of 11.2, reloaded, and the text stays', async () => {
    const element = await openInReplyMode();
    api.changeStatus.and.returnValue(CONFLICT());
    type(composerText(element), 'טקסט הפתרון');
    confirm.confirm.and.returnValue(of(true));
    sendButton(element, 'resolve').click();
    await settle();

    expect(element.querySelector('app-ticket-composer app-form-alert')!.textContent).toContain(CONFLICT_MESSAGE);
    expect(composerText(element).value).toBe('טקסט הפתרון');
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it('422 on a status send: the server\'s message, and the text stays', async () => {
    const element = await openInReplyMode();
    api.changeStatus.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 422,
            error: { type: 'invalid-status-transition', title: 'לא ניתן לעבור מ"ממתינה לעובד" ל"ממתינה לעובד"', status: 422 },
          }),
      ),
    );
    type(composerText(element), 'עוד שאלה');
    sendButton(element, 'requestInfo').click();
    await settle();

    expect(element.querySelector('app-ticket-composer app-form-alert')!.textContent).toContain('לא ניתן לעבור');
    expect(composerText(element).value).toBe('עוד שאלה');
  });

  it('"בקשת מידע" in the actions area moves the writing area to the reply mode', async () => {
    const element = await open(IN_PROGRESS_ACTIONS);
    expect(element.querySelector('app-ticket-composer .composer')!.classList).toContain('mode-internal');

    element.querySelector<HTMLButtonElement>('[data-action="requestInfo"]')!.click();
    await settle();

    expect(element.querySelector('app-ticket-composer .composer')!.classList).toContain('mode-reply');
    expect(sendButtons(element)).toContain('requestInfo');
  });
});
