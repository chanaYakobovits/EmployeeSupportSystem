/** Attachment limits (spec 6.3, 7.4) – same values as the server's Common/Constants/AttachmentLimits. */
export const ATTACHMENT_LIMITS = {
  maxFileBytes: 5 * 1024 * 1024,
  maxFiles: 3,
} as const;

/** Allowed extensions (the server decides the type by the extension of the sanitized name, then checks the content). */
export const ATTACHMENT_EXTENSIONS: readonly string[] = ['pdf', 'jpg', 'jpeg', 'png', 'docx'];

/** The `accept` attribute of the file input. */
export const ATTACHMENT_ACCEPT = ATTACHMENT_EXTENSIONS.map((extension) => `.${extension}`).join(',');

/** An attached file as listed in the timeline (Backend TicketAttachmentDto). */
export interface TicketAttachment {
  id: number;
  fileName: string;
  /** The type detected from the content on upload. */
  contentType: string;
  sizeBytes: number;
  uploadedByName: string;
  createdAt: string;
}

export type AttachmentKind = 'pdf' | 'image' | 'docx' | 'other';

export const ATTACHMENT_ICONS: Readonly<Record<AttachmentKind, string>> = {
  pdf: 'picture_as_pdf',
  image: 'image',
  docx: 'description',
  other: 'attach_file',
};

/** "a.b.PDF" → "pdf"; no extension → "". */
export function fileExtension(fileName: string): string {
  const dot = fileName.lastIndexOf('.');
  return dot < 0 ? '' : fileName.substring(dot + 1).toLowerCase();
}

/** The kind of a file, by its name (a chosen file) or its content type (an uploaded one). */
export function attachmentKind(fileNameOrContentType: string): AttachmentKind {
  const value = fileNameOrContentType.toLowerCase();
  if (value === 'application/pdf') {
    return 'pdf';
  }
  if (value.startsWith('image/')) {
    return 'image';
  }
  if (value === 'application/vnd.openxmlformats-officedocument.wordprocessingml.document') {
    return 'docx';
  }
  switch (fileExtension(value)) {
    case 'pdf':
      return 'pdf';
    case 'jpg':
    case 'jpeg':
    case 'png':
      return 'image';
    case 'docx':
      return 'docx';
    default:
      return 'other';
  }
}
