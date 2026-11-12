import { ANIMATION_MODULE_TYPE } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';

import { TicketsApiService } from '../../../core/api/tickets-api.service';
import { RequesterTicketListItem } from '../../../core/models/ticket.models';
import { fakeTicketItem, pageOf } from '../testing/ticket-fixtures';
import { MyTicketsPage } from './my-tickets-page';

describe('MyTicketsPage', () => {
  let api: jasmine.SpyObj<TicketsApiService>;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    api = jasmine.createSpyObj<TicketsApiService>('TicketsApiService', ['listMine', 'mySummary']);
    api.mySummary.and.returnValue(of({ open: 3, waitingForMe: 1, closedThisMonth: 2 }));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'employee', component: MyTicketsPage }]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: TicketsApiService, useValue: api },
      ],
    });
  });

  async function open(url: string, items: RequesterTicketListItem[] = []): Promise<HTMLElement> {
    api.listMine.and.returnValue(of(pageOf(items)));
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, MyTicketsPage);
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  function rows(element: HTMLElement): HTMLTableRowElement[] {
    return Array.from(element.querySelectorAll<HTMLTableRowElement>('tbody tr'));
  }

  it('shows the three summary cards', async () => {
    const element = await open('/employee');
    const cards = Array.from(element.querySelectorAll('.summary-card')).map((card) => card.textContent!.replace(/\s+/g, ' '));
    expect(cards[0]).toContain('פתוחות');
    expect(cards[0]).toContain('3');
    expect(cards[1]).toContain('ממתינות לתגובתי');
    expect(cards[1]).toContain('1');
    expect(cards[2]).toContain('נסגרו החודש');
    expect(cards[2]).toContain('2');
    expect(element.textContent).not.toContain('דחיפות');
  });

  it('a summary card filters the list by its statuses', async () => {
    const element = await open('/employee');
    const card = element.querySelector<HTMLButtonElement>('.summary-card[data-view="waiting"]')!;
    expect(card.getAttribute('aria-pressed')).toBe('false');

    card.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(api.listMine).toHaveBeenCalledWith(jasmine.objectContaining({ status: ['WaitingForEmployee'], page: 1 }));
    expect(card.getAttribute('aria-pressed')).toBe('true');
  });

  it('the "closed" filter covers closed and cancelled tickets', async () => {
    await open('/employee?view=closed');
    expect(api.listMine).toHaveBeenCalledWith(jasmine.objectContaining({ status: ['Closed', 'Cancelled'] }));
  });

  it('no filter: no status is sent', async () => {
    await open('/employee');
    expect(api.listMine).toHaveBeenCalledWith(jasmine.objectContaining({ status: null, sortBy: 'createdAt', sortDir: 'desc' }));
  });

  it('a description excerpt looks different from a real subject', async () => {
    const element = await open('/employee', [
      fakeTicketItem({ id: 1, subject: 'נושא אמיתי' }),
      fakeTicketItem({ id: 2, subject: 'תחילת התיאור של הפנייה', isSubjectExcerpt: true }),
    ]);
    const [real, excerpt] = rows(element).map((row) => row.querySelector('.subject')!);
    expect(real.classList).not.toContain('excerpt');
    expect(real.textContent!.trim()).toBe('נושא אמיתי');
    expect(excerpt.classList).toContain('excerpt');
    expect(excerpt.textContent).toContain('תחילת התיאור של הפנייה…');
    expect(excerpt.textContent).toContain('תקציר התיאור');
  });

  it('no category yet: "מתבצע שיוך..."; no handler: "טרם שויך"', async () => {
    const element = await open('/employee', [fakeTicketItem({ categoryName: null, handlerName: null, status: 'New' })]);
    const text = rows(element)[0].textContent!;
    expect(text).toContain('מתבצע שיוך...');
    expect(text).toContain('טרם שויך');
    expect(text).toContain('חדשה');
  });

  it('a ticket waiting for the employee is highlighted', async () => {
    const element = await open('/employee', [
      fakeTicketItem({ id: 1, status: 'WaitingForEmployee' }),
      fakeTicketItem({ id: 2, status: 'InProgress' }),
    ]);
    const [waiting, other] = rows(element);
    expect(waiting.classList).toContain('row-attention');
    expect(waiting.textContent).toContain('נדרשת תגובתך');
    expect(other.classList).not.toContain('row-attention');
  });

  it('the ticket number links to the ticket', async () => {
    const element = await open('/employee', [fakeTicketItem({ id: 77 })]);
    expect(rows(element)[0].querySelector('a.ticket-number')!.getAttribute('href')).toBe('/employee/tickets/77');
  });

  it('no tickets and no filter: "אין לך פניות עדיין" with a "new ticket" action', async () => {
    const element = await open('/employee');
    const empty = element.querySelector('app-empty-state')!;
    expect(empty.textContent).toContain('אין לך פניות עדיין');
    expect(empty.querySelector('a')!.getAttribute('href')).toBe('/employee/tickets/new');
  });

  it('no results with a filter: "לא נמצאו פניות" with "clear filters"', async () => {
    const element = await open('/employee?search=מדפסת');
    const empty = element.querySelector('app-empty-state')!;
    expect(empty.textContent).toContain('לא נמצאו פניות');
    expect(empty.querySelector('button')!.textContent).toContain('ניקוי סינון');
  });
});
