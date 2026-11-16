import { TicketStatus } from '../models/ticket.models';
import { includesFinalStatuses, withFinalStatuses } from './ticket-queue.store';

describe('the "כולל סגורות ומבוטלות" filter', () => {
  const all: TicketStatus[] = ['New', 'Assigned', 'InProgress', 'WaitingForEmployee', 'Resolved', 'Closed', 'Cancelled'];

  it('is on when the status filter holds a closed or cancelled status', () => {
    expect(includesFinalStatuses(null)).toBeFalse();
    expect(includesFinalStatuses(['New', 'Resolved'])).toBeFalse();
    expect(includesFinalStatuses(['Cancelled'])).toBeTrue();
  });

  it('on: the selected statuses (none = all) and also Closed and Cancelled, in workflow order', () => {
    expect(withFinalStatuses(null, true)).toEqual(all);
    expect(withFinalStatuses(['Resolved', 'New'], true)).toEqual(['New', 'Resolved', 'Closed', 'Cancelled']);
  });

  it('off: without them, and the default (null) when nothing or every other status is left', () => {
    expect(withFinalStatuses(['New', 'Closed'], false)).toEqual(['New']);
    expect(withFinalStatuses(['Closed', 'Cancelled'], false)).toBeNull();
    expect(withFinalStatuses(all, false)).toBeNull();
    expect(withFinalStatuses(null, false)).toBeNull();
  });
});
