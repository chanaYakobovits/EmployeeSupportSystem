/** Wait this long after the last keystroke before announcing the character count to screen readers. */
export const COUNTER_ANNOUNCE_DELAY_MS = 800;

const NUMBER = new Intl.NumberFormat('he-IL');

/** "4,000". */
export function formatCount(value: number): string {
  return NUMBER.format(value);
}

/**
 * The screen-reader text of a character counter (spec 11.6): "נותרו 3,950 תווים" / "נדרשים עוד 4 תווים לפחות".
 * The minimum is counted after trimming, like the server. '' for an empty field.
 */
export function describeCount(value: string, limits: { min?: number; max: number }): string {
  if (value.length === 0) {
    return '';
  }
  const trimmedLength = value.trim().length;
  const min = limits.min ?? 0;
  if (trimmedLength < min) {
    const missing = min - trimmedLength;
    return missing === 1 ? 'נדרש עוד תו אחד לפחות' : `נדרשים עוד ${missing} תווים לפחות`;
  }
  const remaining = limits.max - value.length;
  if (remaining <= 0) {
    return 'הגעת למספר התווים המרבי';
  }
  return remaining === 1 ? 'נותר תו אחד' : `נותרו ${formatCount(remaining)} תווים`;
}
