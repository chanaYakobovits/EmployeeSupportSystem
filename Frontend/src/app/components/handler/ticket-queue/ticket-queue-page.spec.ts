import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';

import { PagedResult } from '../../../models/paging.models';
import { BulkUpdateResult, StaffTicketListItem } from '../../../models/ticket.models';
import { AuthService } from '../../../services/auth.service';
import { CategoriesApiService } from '../../../services/categories-api.service';
import { NotificationService } from '../../../services/notification.service';
import { TicketsApiService } from '../../../services/tickets-api.service';
import { pageOf } from '../../employee/testing/ticket-fixtures';
import { HANDLER_ROUTES } from '../handler.routes';
import { CONFLICT_MESSAGE } from '../handler-messages';
import { HANDLER_ID, fakeAuth, fakeStaffItem } from '../testing/handler-fixtures';
import { TicketQueuePage } from './ticket-queue-page';

@Component({ template: '' })
class Stub {}

describe('TicketQueuePage', () => {
  let api: jasmine.SpyObj<TicketsApiService>;
  let notifications: jasmine.SpyObj<NotificationService>;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    api = jasmine.createSpyObj<TicketsApiService>('TicketsApiService', [
      'listStaff',
      'assign',
      'bulkChangeStatus',
      'bulkAssign',
      'bulkAssignOptions',
    ]);
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'error']);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          // The ticket screen is a stub here (the queue's own routes otherwise).
          { path: 'handler/tickets/:id', component: Stub },
          { path: 'handler', children: HANDLER_ROUTES },
        ]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: TicketsApiService, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: AuthService, useValue: fakeAuth() },
        { provide: CategoriesApiService, useValue: { list: () => of([]) } },
      ],
    });
  });

  async function open(
    url: string,
    items: StaffTicketListItem[] = [],
    page: PagedResult<StaffTicketListItem> = pageOf(items),
  ): Promise<HTMLElement> {
    api.listStaff.and.returnValue(of(page));
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, TicketQueuePage);
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  function rows(element: HTMLElement): HTMLTableRowElement[] {
    return Array.from(element.querySelectorAll<HTMLTableRowElement>('tbody tr'));
  }

  it('lands on "משויכות אליי": scope=assigned, by priority and without a status filter (no closed ones)', async () => {
    await open('/handler');

    expect(TestBed.inject(Router).url).toBe('/handler/assigned');
    expect(api.listStaff).toHaveBeenCalledWith(
      jasmine.objectContaining({ scope: 'assigned', sortBy: 'priority', sortDir: 'asc', page: 1, status: null }),
    );
  });

  function finalToggle(element: HTMLElement): HTMLButtonElement {
    const toggle = Array.from(element.querySelectorAll('mat-slide-toggle')).find((t) =>
      t.textContent!.includes('כולל סגורות ומבוטלות'),
    )!;
    return toggle.querySelector<HTMLButtonElement>('button[role="switch"]')!;
  }

  it('"כולל סגורות ומבוטלות" asks for every status, and switching it off goes back to the default', async () => {
    const element = await open('/handler/queue');
    const router = TestBed.inject(Router);
    expect(finalToggle(element).getAttribute('aria-checked')).toBe('false');

    finalToggle(element).click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(router.url).toBe('/handler/queue?status=New,Assigned,InProgress,WaitingForEmployee,Resolved,Closed,Cancelled');
    expect(api.listStaff).toHaveBeenCalledWith(
      jasmine.objectContaining({
        status: ['New', 'Assigned', 'InProgress', 'WaitingForEmployee', 'Resolved', 'Closed', 'Cancelled'],
      }),
    );
    expect(finalToggle(element).getAttribute('aria-checked')).toBe('true');

    finalToggle(element).click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/handler/queue');
  });

  it('an explicit closed status in the URL switches "כולל סגורות ומבוטלות" on', async () => {
    const element = await open('/handler/queue?status=Closed');

    expect(api.listStaff).toHaveBeenCalledWith(jasmine.objectContaining({ status: ['Closed'] }));
    expect(finalToggle(element).getAttribute('aria-checked')).toBe('true');
  });

  it('the sort select offers "עדיפות" and the other fields; a field goes to the URL in its usual direction', async () => {
    const element = await open('/handler/assigned');
    const sortField = Array.from(element.querySelectorAll('mat-form-field')).find(
      (f) => f.querySelector('mat-label')?.textContent?.trim() === 'מיון',
    )!;
    expect(sortField.textContent).toContain('עדיפות');

    sortField.querySelector<HTMLElement>('.mat-mdc-select-trigger')!.click();
    harness.detectChanges();
    const options = Array.from(document.querySelectorAll<HTMLElement>('mat-option'));
    expect(options.map((o) => o.textContent!.trim())).toEqual([
      'עדיפות',
      'מועד יעד',
      'דחיפות',
      'תאריך פתיחה',
      'עדכון אחרון',
      'סטטוס',
    ]);
    options.find((o) => o.textContent!.trim() === 'עדכון אחרון')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/handler/assigned?sortBy=updatedAt');
    expect(api.listStaff).toHaveBeenCalledWith(jasmine.objectContaining({ sortBy: 'updatedAt', sortDir: 'desc' }));
  });

  for (const [path, scope] of [
    ['queue', 'queue'],
    ['handling', 'handling'],
  ] as const) {
    it(`the tab in the URL (/handler/${path}) is the scope of the request, and it is the active tab`, async () => {
      const element = await open(`/handler/${path}`);

      expect(api.listStaff).toHaveBeenCalledWith(jasmine.objectContaining({ scope }));
      const active = element.querySelector('a[mat-tab-link].mdc-tab--active, a[mat-tab-link][aria-current="page"]')!;
      expect(active.getAttribute('data-scope')).toBe(scope);
    });
  }

  it('the tab links keep the filters, without the page', async () => {
    const element = await open('/handler/assigned?search=מדפסת&page=3&overdueOnly=true');
    const queueTab = element.querySelector<HTMLAnchorElement>('a[data-scope="queue"]')!;

    expect(queueTab.getAttribute('href')).toBe(`/handler/queue?search=${encodeURIComponent('מדפסת')}&overdueOnly=true`);
  });

  it('filters from the URL go to the server: several statuses and urgencies (incl. NotSet), dates, overdue only', async () => {
    await open('/handler/handling?status=New,InProgress&urgency=High,NotSet&from=2026-11-01&to=2026-11-15&overdueOnly=true');

    expect(api.listStaff).toHaveBeenCalledWith(
      jasmine.objectContaining({
        scope: 'handling',
        status: ['New', 'InProgress'],
        urgency: ['High', 'NotSet'],
        from: '2026-11-01',
        to: '2026-11-15',
        overdueOnly: true,
      }),
    );
  });

  it('shows the columns of spec 7.6 with the countdown and the classification mark', async () => {
    const element = await open('/handler/assigned', [fakeStaffItem({ classificationSource: 'Fallback', urgency: null })]);
    // The sortable headers also hold the sort icon's ligature.
    const headers = Array.from(element.querySelectorAll('thead th')).map((th) =>
      th.textContent!.replace(/unfold_more|arrow_upward|arrow_downward/, '').trim(),
    );
    expect(headers).toEqual([
      '',
      "מס'",
      'נושא',
      'שם העובד',
      'מחלקת העובד',
      'קטגוריה',
      'דחיפות',
      'סטטוס',
      'מועד יעד',
      'מקור סיווג',
    ]);

    const row = rows(element)[0].textContent!;
    expect(row).toContain('#152');
    expect(row).toContain('נועה ברקוביץ');
    expect(row).toContain('פיתוח');
    expect(row).toContain('לא נקבעה');
    expect(row).toContain('נותרו 3 שעות');
    expect(row).toContain('גיבוי');
  });

  it('an overdue row is red, and says so with an icon and text', async () => {
    const element = await open('/handler/assigned', [
      fakeStaffItem({ id: 1, isOverdue: true, dueAt: new Date(Date.now() - 2 * 3_600_000 - 60_000).toISOString() }),
      fakeStaffItem({ id: 2 }),
    ]);
    const [overdue, onTime] = rows(element);

    expect(overdue.classList).toContain('row-overdue');
    expect(overdue.querySelector('.overdue-mark')!.textContent).toContain('חריגה');
    expect(overdue.querySelector('.overdue-mark mat-icon')).not.toBeNull();
    expect(overdue.textContent).toContain('חריגה של שעתיים');
    expect(onTime.classList).not.toContain('row-overdue');
    expect(onTime.querySelector('.overdue-mark')).toBeNull();
  });

  it('waiting for the employee: no countdown and no overdue mark', async () => {
    const element = await open('/handler/assigned', [
      fakeStaffItem({ status: 'WaitingForEmployee', dueAt: new Date(Date.now() - 3_600_000).toISOString() }),
    ]);
    const row = rows(element)[0];
    expect(row.classList).not.toContain('row-overdue');
    expect(row.textContent).not.toContain('נותרו');
    expect(row.textContent).not.toContain('חריגה');
  });

  it('no "קח לטיפולי" outside the queue tab', async () => {
    const element = await open('/handler/assigned', [fakeStaffItem()]);
    expect(element.querySelector('.take-button')).toBeNull();
  });

  it('"קח לטיפולי" in the queue tab assigns the ticket to the signed-in handler', async () => {
    const element = await open('/handler/queue', [fakeStaffItem({ id: 152, rowVersion: 'AAAAAAAAB9E=' })]);
    api.assign.and.returnValue(of({ id: 152, rowVersion: 'AAAAAAAAB9F=', canView: true }));
    const calls = api.listStaff.calls.count();
    element.querySelector<HTMLButtonElement>('.take-button')!.click();
    harness.detectChanges();

    expect(api.assign).toHaveBeenCalledWith(152, { targetUserId: HANDLER_ID, rowVersion: 'AAAAAAAAB9E=' });
    expect(notifications.success).toHaveBeenCalled();
    expect(api.listStaff.calls.count()).toBe(calls + 1);
  });

  it('"קח לטיפולי" after someone else changed the ticket (409): the message of 11.2 and the list is reloaded', async () => {
    const element = await open('/handler/queue', [fakeStaffItem()]);
    api.assign.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    const calls = api.listStaff.calls.count();
    element.querySelector<HTMLButtonElement>('.take-button')!.click();
    harness.detectChanges();

    expect(notifications.error).toHaveBeenCalledWith(CONFLICT_MESSAGE);
    expect(api.listStaff.calls.count()).toBe(calls + 1);
  });

  it('the empty queue says so', async () => {
    const element = await open('/handler/queue');
    expect(element.querySelector('app-empty-state')!.textContent).toContain('התור ריק');
  });

  it('no results with a filter offers to clear it', async () => {
    const element = await open('/handler/queue?search=xyz');
    expect(element.querySelector('app-empty-state')!.textContent).toContain('לא נמצאו פניות');
  });

  it('the ticket number links to the handling screen', async () => {
    const element = await open('/handler/assigned', [fakeStaffItem({ id: 77 })]);
    expect(rows(element)[0].querySelector('a.ticket-number')!.getAttribute('href')).toBe('/handler/tickets/77');
  });

  // ===================== Bulk selection and actions (task 3.6) =====================

  function checkbox(element: HTMLElement, id: number): HTMLInputElement {
    return element.querySelector<HTMLInputElement>(`tbody mat-checkbox[data-ticket="${id}"] input`)!;
  }

  function selectAll(element: HTMLElement): HTMLInputElement {
    return element.querySelector<HTMLInputElement>('thead mat-checkbox input')!;
  }

  function bar(element: HTMLElement): HTMLElement | null {
    return element.querySelector('app-bulk-action-bar');
  }

  function barButton(element: HTMLElement, action: 'start' | 'assign' | 'clear'): HTMLButtonElement {
    return bar(element)!.querySelector<HTMLButtonElement>(`button[data-action="${action}"]`)!;
  }

  const threeOnPage = (): StaffTicketListItem[] => [
    fakeStaffItem({ id: 1042, rowVersion: 'AAAAAAAAAAE=' }),
    fakeStaffItem({ id: 1043, rowVersion: 'AAAAAAAAAAI=' }),
    fakeStaffItem({ id: 1044, rowVersion: 'AAAAAAAAAAM=' }),
  ];

  it('every row has a checkbox named after its ticket, and the header one selects the page', async () => {
    const element = await open('/handler/assigned', threeOnPage());

    expect(checkbox(element, 1042).getAttribute('aria-label')).toBe('בחר פנייה 1042');
    expect(selectAll(element).getAttribute('aria-label')).toBe('בחירת כל הפניות בעמוד');
    expect(bar(element)).toBeNull();
  });

  it('"select all" selects the rows of this page only, not every result', async () => {
    // 3 rows shown out of 60 results.
    const items = threeOnPage();
    const element = await open('/handler/assigned', items, { items, page: 1, pageSize: 25, totalCount: 60 });

    selectAll(element).click();
    harness.detectChanges();

    expect(bar(element)!.textContent).toContain('נבחרו 3 פניות');
    expect(rows(element).every((tr) => tr.classList.contains('row-selected'))).toBeTrue();

    api.bulkChangeStatus.and.returnValue(of({ succeeded: [1042, 1043, 1044], failed: [] }));
    barButton(element, 'start').click();
    harness.detectChanges();

    expect(api.bulkChangeStatus).toHaveBeenCalledWith({
      newStatus: 'InProgress',
      items: [
        { id: 1042, rowVersion: 'AAAAAAAAAAE=' },
        { id: 1043, rowVersion: 'AAAAAAAAAAI=' },
        { id: 1044, rowVersion: 'AAAAAAAAAAM=' },
      ],
    });
  });

  it('selecting a row shows the bar (a region, its count announced); "ניקוי בחירה" clears it', async () => {
    const element = await open('/handler/assigned', threeOnPage());

    checkbox(element, 1043).click();
    harness.detectChanges();

    expect(bar(element)!.textContent).toContain('נבחרה פנייה אחת');
    expect(bar(element)!.querySelector('[role="region"]')!.getAttribute('aria-label')).toBe('פעולות על הפניות שנבחרו');
    expect(bar(element)!.querySelector('[aria-live="polite"]')!.textContent).toContain('נבחרה פנייה אחת');
    expect(selectAll(element).indeterminate).toBeTrue();

    barButton(element, 'clear').click();
    harness.detectChanges();

    expect(bar(element)).toBeNull();
    expect(checkbox(element, 1043).checked).toBeFalse();
  });

  for (const [what, url] of [
    ['another page', '/handler/assigned?page=2'],
    ['a filter', '/handler/assigned?search=xyz'],
    ['another sort', '/handler/assigned?sortBy=dueAt'],
  ] as const) {
    it(`${what} clears the selection`, async () => {
      const items = threeOnPage();
      const element = await open('/handler/assigned', items, { items, page: 1, pageSize: 25, totalCount: 60 });
      checkbox(element, 1042).click();
      harness.detectChanges();
      expect(bar(element)).not.toBeNull();

      await harness.navigateByUrl(url);
      harness.detectChanges();

      expect(bar(element)).toBeNull();
      expect(checkbox(element, 1042).checked).toBeFalse();
    });
  }

  it('a reload of the same view keeps the selection', async () => {
    const element = await open('/handler/queue', threeOnPage());
    checkbox(element, 1042).click();
    harness.detectChanges();
    api.assign.and.returnValue(of({ id: 1043, rowVersion: 'x', canView: true }));
    const calls = api.listStaff.calls.count();

    // "קח לטיפולי" on another row reloads the list.
    element.querySelectorAll<HTMLButtonElement>('.take-button')[1].click();
    harness.detectChanges();

    expect(api.listStaff.calls.count()).toBe(calls + 1);
    expect(checkbox(element, 1042).checked).toBeTrue();
  });

  it('after "התחל טיפול": the result dialog with the Hebrew reasons, and only the failures stay selected', async () => {
    const element = await open('/handler/assigned', threeOnPage());
    selectAll(element).click();
    harness.detectChanges();
    const result: BulkUpdateResult = {
      succeeded: [1042],
      failed: [
        { id: 1043, reason: 'Conflict', errorType: 'concurrency' },
        { id: 1044, reason: 'InvalidTransition', errorType: 'invalid-status-transition' },
      ],
    };
    api.bulkChangeStatus.and.returnValue(of(result));
    const calls = api.listStaff.calls.count();

    barButton(element, 'start').click();
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();

    // The list was reloaded; the rows that failed are still selected.
    expect(api.listStaff.calls.count()).toBe(calls + 1);
    expect(checkbox(element, 1042).checked).toBeFalse();
    expect(checkbox(element, 1043).checked).toBeTrue();
    expect(checkbox(element, 1044).checked).toBeTrue();
    expect(bar(element)!.textContent).toContain('נבחרו 2 פניות');

    const dialog = document.querySelector('app-bulk-result-dialog')!;
    expect(dialog.textContent).toContain('פנייה אחת עודכנה בהצלחה, 2 פניות לא עודכנו');
    const failures = Array.from(dialog.querySelectorAll('li.failure')).map((li) => [
      li.querySelector('a')!.textContent!.replace(/\s+/g, ' ').trim(),
      li.querySelector('.reason')!.textContent!.trim(),
    ]);
    expect(failures).toEqual([
      ['פנייה מספר #1043', 'עודכנה על ידי משתמש אחר'],
      ['פנייה מספר #1044', 'לא ניתן להתחיל טיפול בסטטוס הנוכחי'],
    ]);
    expect(dialog.querySelector('a.ticket-number')!.getAttribute('href')).toBe('/handler/tickets/1043');
  });

  it('a failed bulk request: an error message, the list is reloaded and the selection kept', async () => {
    const element = await open('/handler/assigned', threeOnPage());
    checkbox(element, 1042).click();
    harness.detectChanges();
    api.bulkChangeStatus.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const calls = api.listStaff.calls.count();

    barButton(element, 'start').click();
    harness.detectChanges();

    expect(notifications.error).toHaveBeenCalled();
    expect(api.listStaff.calls.count()).toBe(calls + 1);
    expect(checkbox(element, 1042).checked).toBeTrue();
  });

  it('"העברה…" opens the transfer dialog for the selected tickets', async () => {
    const element = await open('/handler/assigned', threeOnPage());
    api.bulkAssignOptions.and.returnValue(of({ handlers: [], departments: [] }));
    checkbox(element, 1042).click();
    checkbox(element, 1044).click();
    harness.detectChanges();

    barButton(element, 'assign').click();
    harness.detectChanges();
    await harness.fixture.whenStable();

    const dialogs = document.querySelectorAll('app-bulk-assign-dialog');
    expect(dialogs[dialogs.length - 1].querySelector('h2')!.textContent).toContain('העברת 2 פניות');
  });
});
