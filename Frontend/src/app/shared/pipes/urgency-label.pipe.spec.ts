import { UrgencyLabelPipe } from './urgency-label.pipe';

describe('UrgencyLabelPipe', () => {
  const pipe = new UrgencyLabelPipe();

  it('translates every urgency to Hebrew', () => {
    expect(pipe.transform('Critical')).toBe('קריטית');
    expect(pipe.transform('High')).toBe('גבוהה');
    expect(pipe.transform('Medium')).toBe('בינונית');
    expect(pipe.transform('Low')).toBe('נמוכה');
  });

  it('returns an empty string for no urgency (not analyzed yet)', () => {
    expect(pipe.transform(null)).toBe('');
  });
});
