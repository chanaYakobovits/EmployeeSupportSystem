import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';

import { StaffTicketListItem } from '../../../models/ticket.models';
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
    api = jasmine.createSpyObj<TicketsApiService>('TicketsApiService', ['listStaff', 'assign']);
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

  async function open(url: string, items: StaffTicketListItem[] = []): Promise<HTMLElement> {
    api.listStaff.and.returnValue(of(pageOf(items)));
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
});
