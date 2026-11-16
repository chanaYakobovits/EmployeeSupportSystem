import { LiveAnnouncer } from '@angular/cdk/a11y';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';

import { BulkAction, BulkFailure, BulkUpdateResult } from '../../../../models/ticket.models';
import { bulkFailureText } from '../../handler-messages';
import { BulkResultDialog } from './bulk-result-dialog';

describe('BulkResultDialog', () => {
  let announcer: jasmine.SpyObj<LiveAnnouncer>;

  function render(action: BulkAction, result: BulkUpdateResult): HTMLElement {
    announcer = jasmine.createSpyObj<LiveAnnouncer>('LiveAnnouncer', ['announce']);
    announcer.announce.and.resolveTo();
    TestBed.configureTestingModule({
      imports: [BulkResultDialog],
      providers: [
        provideRouter([]),
        { provide: MAT_DIALOG_DATA, useValue: { action, result, queueUrl: '/handler/queue?page=2' } },
        { provide: MatDialogRef, useValue: { close: () => undefined } },
        { provide: LiveAnnouncer, useValue: announcer },
      ],
    });
    const fixture = TestBed.createComponent(BulkResultDialog);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('all succeeded: the count only, announced', () => {
    const element = render('start', { succeeded: [1, 2, 3], failed: [] });

    expect(element.querySelector('#bulk-result-summary')!.textContent).toContain('3 פניות עודכנו בהצלחה');
    expect(element.querySelector('li.failure')).toBeNull();
    expect(announcer.announce).toHaveBeenCalledWith('3 פניות עודכנו בהצלחה', 'polite');
  });

  it('lists every failure with a link to the ticket and the reason in Hebrew', () => {
    const element = render('assign', {
      succeeded: [],
      failed: [
        { id: 1050, reason: 'NotFound', errorType: null },
        { id: 1077, reason: 'InvalidTarget', errorType: 'assignee-is-requester' },
      ],
    });

    expect(element.querySelector('#bulk-result-summary')!.textContent).toContain('אף פנייה לא עודכנה');
    const items = Array.from(element.querySelectorAll('li.failure'));
    expect(items.map((li) => li.querySelector('.reason')!.textContent!.trim())).toEqual([
      'לא נמצאה או שאין לך גישה אליה',
      'המטפל שנבחר הוא העובד שפתח את הפנייה',
    ]);
    expect(items[1].querySelector('a')!.getAttribute('href')).toBe('/handler/tickets/1077');
    expect(announcer.announce).toHaveBeenCalledWith(
      'אף פנייה לא עודכנה. הפניות שלא עודכנו נשארו מסומנות ברשימה.',
      'polite',
    );
  });
});

describe('bulkFailureText', () => {
  const failure = (reason: BulkFailure['reason'], errorType: string | null = null): BulkFailure => ({
    id: 1,
    reason,
    errorType,
  });

  it('says why in Hebrew for every reason, refined by the error type', () => {
    expect(bulkFailureText(failure('Conflict', 'concurrency'), 'start')).toBe('עודכנה על ידי משתמש אחר');
    expect(bulkFailureText(failure('NotFound'), 'assign')).toBe('לא נמצאה או שאין לך גישה אליה');
    expect(bulkFailureText(failure('InvalidTransition', 'invalid-status-transition'), 'start')).toBe(
      'לא ניתן להתחיל טיפול בסטטוס הנוכחי',
    );
    expect(bulkFailureText(failure('InvalidTransition', 'ticket-assigned-to-other'), 'start')).toBe('משויכת למטפל אחר');
    expect(bulkFailureText(failure('InvalidTransition', 'ticket-not-open'), 'assign')).toBe(
      'לא ניתן להעביר פנייה שנפתרה, נסגרה או בוטלה',
    );
    expect(bulkFailureText(failure('InvalidTarget', 'assignment-unchanged'), 'assign')).toBe(
      'הפנייה כבר משויכת ליעד שנבחר',
    );
    expect(bulkFailureText(failure('InvalidTarget', 'something-new'), 'assign')).toBe('היעד שנבחר אינו מתאים לפנייה');
  });
});
