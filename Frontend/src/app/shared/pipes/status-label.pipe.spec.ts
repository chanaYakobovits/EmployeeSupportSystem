import { TICKET_STATUS_VALUES } from '../../core/models/ticket.models';
import { StatusLabelPipe } from './status-label.pipe';

describe('StatusLabelPipe', () => {
  const pipe = new StatusLabelPipe();

  it('translates the 7 statuses to Hebrew (spec 3.2)', () => {
    expect(TICKET_STATUS_VALUES.map((status) => pipe.transform(status))).toEqual([
      'חדשה',
      'שויכה',
      'בטיפול',
      'ממתינה לעובד',
      'נפתרה',
      'סגורה',
      'בוטלה',
    ]);
  });

  it('returns an empty string for no status', () => {
    expect(pipe.transform(null)).toBe('');
  });
});
