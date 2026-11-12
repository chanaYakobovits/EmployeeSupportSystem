import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';

import { TicketsApiService } from '../../../core/api/tickets-api.service';
import { fakeTicketDetails } from '../testing/ticket-fixtures';
import { TicketViewPage } from './ticket-view-page';

describe('TicketViewPage', () => {
  let api: jasmine.SpyObj<TicketsApiService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<TicketsApiService>('TicketsApiService', ['get']);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'employee/tickets/:id', component: TicketViewPage }], withComponentInputBinding()),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: TicketsApiService, useValue: api },
      ],
    });
  });

  async function open(url: string): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, TicketViewPage);
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  it('shows the number, subject, status, and "מתבצע שיוך..." / "טרם שויך" before classification', async () => {
    api.get.and.returnValue(of(fakeTicketDetails()));
    const element = await open('/employee/tickets/1042');

    expect(api.get).toHaveBeenCalledWith(1042);
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
    const element = await open('/employee/tickets/1042');

    const shown = element.querySelector<HTMLElement>('.description')!;
    expect(shown.querySelector('b')).toBeNull();
    expect(shown.querySelector('img')).toBeNull();
    expect(shown.textContent).toBe(description);
    expect(getComputedStyle(shown).whiteSpace).toBe('pre-wrap');
  });

  it('the timeline: the opening and every status change, the system when no one', async () => {
    api.get.and.returnValue(
      of(
        fakeTicketDetails({
          status: 'Assigned',
          timeline: [
            { previousStatus: null, newStatus: 'New', changedAt: '2026-11-10T08:00:00Z', changedByName: 'ישראל ישראלי' },
            { previousStatus: 'New', newStatus: 'Assigned', changedAt: '2026-11-10T08:01:00Z', changedByName: null },
          ],
        }),
      ),
    );
    const element = await open('/employee/tickets/1042');
    const entries = Array.from(element.querySelectorAll('app-ticket-timeline li')).map((li) => li.textContent!);
    expect(entries.length).toBe(2);
    expect(entries[0]).toContain('הפנייה נפתחה');
    expect(entries[0]).toContain('ישראל ישראלי');
    expect(entries[1]).toContain('הסטטוס שונה ל-');
    expect(entries[1]).toContain('שויכה');
    expect(entries[1]).toContain('מערכת');
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
    expect(element.textContent).toContain('הפנייה לא נמצאה');
  });

  it('another error: the error state with "נסה שוב"', async () => {
    api.get.and.returnValues(
      throwError(() => new HttpErrorResponse({ status: 500 })),
      of(fakeTicketDetails()),
    );
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/employee/tickets/1042', TicketViewPage);
    harness.detectChanges();
    const element = harness.routeNativeElement!;
    element.querySelector<HTMLButtonElement>('app-error-state button')!.click();
    harness.detectChanges();
    expect(api.get).toHaveBeenCalledTimes(2);
    expect(element.querySelector('h1')!.textContent).toContain('המחשב לא נדלק');
  });
});
