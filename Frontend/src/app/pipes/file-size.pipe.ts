import { Pipe, PipeTransform } from '@angular/core';

const KB = 1024;
const MB = 1024 * 1024;

/**
 * "350 B", "350 KB", "1.2 MB", "5 MB" – base 1024, like the server's limit (5MB = 5 × 1024 × 1024).
 * Show it with dir="ltr" so the unit stays after the number in RTL text.
 */
export function formatFileSize(bytes: number | null | undefined): string {
  if (bytes === null || bytes === undefined || !Number.isFinite(bytes) || bytes < 0) {
    return '';
  }
  if (bytes < KB) {
    return `${bytes} B`;
  }
  const kilobytes = Math.round(bytes / KB);
  if (kilobytes < KB) {
    return `${kilobytes} KB`;
  }
  // One decimal, without a trailing ".0".
  return `${Number((bytes / MB).toFixed(1))} MB`;
}

@Pipe({ name: 'fileSize' })
export class FileSizePipe implements PipeTransform {
  transform(bytes: number | null | undefined): string {
    return formatFileSize(bytes);
  }
}
