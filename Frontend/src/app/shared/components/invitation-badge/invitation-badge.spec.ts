import { TestBed } from '@angular/core/testing';

import { INVITATION_STATUS_VALUES, InvitationStatus } from '../../../core/models/user.models';
import { INVITATION_STATUSES, INVITATION_STATUS_ORDER, InvitationBadge } from './invitation-badge';

describe('InvitationBadge', () => {
  function render(status: InvitationStatus | null): HTMLElement {
    const fixture = TestBed.createComponent(InvitationBadge);
    fixture.componentRef.setInput('status', status);
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  const expected: [InvitationStatus, string, string][] = [
    ['Pending', 'ממתינה', 'tone-accent'],
    ['Sent', 'נשלחה', 'tone-primary'],
    ['Used', 'נוצלה', 'tone-success'],
    ['Expired', 'פגה', 'tone-warning'],
    ['Failed', 'נכשלה', 'tone-danger'],
    ['Revoked', 'בוטלה', 'tone-neutral'],
  ];

  for (const [status, label, tone] of expected) {
    it(`${status} → "${label}" (${tone})`, () => {
      const badge = render(status).querySelector('app-badge')!;
      expect(badge.textContent!.trim()).toBe(label);
      expect(badge.classList).toContain(tone);
    });
  }

  it('never invited → "—" with an accessible label', () => {
    const element = render(null);
    expect(element.querySelector('app-badge')).toBeNull();
    expect(element.textContent!.trim()).toBe('—');
    expect(element.querySelector('[aria-label]')!.getAttribute('aria-label')).toBe('לא נשלחה הזמנה');
  });

  it('every server status has a label, and the filter lists each once', () => {
    expect(Object.keys(INVITATION_STATUSES).sort()).toEqual([...INVITATION_STATUS_VALUES].sort());
    expect([...INVITATION_STATUS_ORDER].sort()).toEqual([...INVITATION_STATUS_VALUES].sort());
  });
});
