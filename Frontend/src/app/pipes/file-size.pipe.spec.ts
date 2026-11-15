import { formatFileSize } from './file-size.pipe';

describe('formatFileSize', () => {
  it('bytes under 1KB', () => {
    expect(formatFileSize(0)).toBe('0 B');
    expect(formatFileSize(350)).toBe('350 B');
  });

  it('kilobytes, rounded', () => {
    expect(formatFileSize(350 * 1024)).toBe('350 KB');
    expect(formatFileSize(1536)).toBe('2 KB');
  });

  it('megabytes with one decimal, without ".0"', () => {
    expect(formatFileSize(1.2 * 1024 * 1024)).toBe('1.2 MB');
    expect(formatFileSize(5 * 1024 * 1024)).toBe('5 MB');
  });

  it('just under 1MB is shown as 1 MB, not 1024 KB', () => {
    expect(formatFileSize(1024 * 1024 - 100)).toBe('1 MB');
  });

  it('no value', () => {
    expect(formatFileSize(null)).toBe('');
    expect(formatFileSize(-1)).toBe('');
  });
});
