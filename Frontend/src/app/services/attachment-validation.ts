import { ATTACHMENT_EXTENSIONS, ATTACHMENT_LIMITS, fileExtension } from '../models/attachment.models';

/**
 * The server's messages (Services/Validators/AttachmentFilesValidator), so the employee sees the same reason before
 * sending. The content check ("תוכן הקובץ אינו תואם לסוג הקובץ") stays on the server.
 */
export const FILE_REJECTION_MESSAGES = {
  empty: 'הקובץ ריק',
  tooLarge: `גודל כל קובץ: עד ${ATTACHMENT_LIMITS.maxFileBytes / (1024 * 1024)}MB`,
  typeNotAllowed: 'ניתן לצרף רק קבצי PDF, JPG, PNG או DOCX',
  duplicate: 'הקובץ כבר נבחר',
  tooMany: `ניתן לצרף עד ${ATTACHMENT_LIMITS.maxFiles} קבצים`,
} as const;

export interface RejectedFile {
  fileName: string;
  reason: string;
}

export interface FileValidationResult {
  /** The files to add, in the order they were chosen. */
  accepted: File[];
  rejected: RejectedFile[];
}

/**
 * Checks newly chosen files against the ones already chosen: each one in the server's order (empty, size, type),
 * then duplicates and the count – files beyond the 3rd are rejected, the first ones are kept.
 */
export function validateFiles(current: readonly File[], incoming: readonly File[]): FileValidationResult {
  const accepted: File[] = [];
  const rejected: RejectedFile[] = [];
  for (const file of incoming) {
    const reason = rejectionReason(file, [...current, ...accepted]);
    if (reason) {
      rejected.push({ fileName: file.name, reason });
    } else {
      accepted.push(file);
    }
  }
  return { accepted, rejected };
}

function rejectionReason(file: File, chosen: readonly File[]): string | null {
  if (file.size === 0) {
    return FILE_REJECTION_MESSAGES.empty;
  }
  if (file.size > ATTACHMENT_LIMITS.maxFileBytes) {
    return FILE_REJECTION_MESSAGES.tooLarge;
  }
  if (!ATTACHMENT_EXTENSIONS.includes(fileExtension(file.name))) {
    return FILE_REJECTION_MESSAGES.typeNotAllowed;
  }
  if (chosen.some((other) => isSameFile(other, file))) {
    return FILE_REJECTION_MESSAGES.duplicate;
  }
  if (chosen.length >= ATTACHMENT_LIMITS.maxFiles) {
    return FILE_REJECTION_MESSAGES.tooMany;
  }
  return null;
}

function isSameFile(a: File, b: File): boolean {
  return a.name === b.name && a.size === b.size && a.lastModified === b.lastModified;
}
