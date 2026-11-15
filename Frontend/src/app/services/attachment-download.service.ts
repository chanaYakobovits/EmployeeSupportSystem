import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, EMPTY, catchError, map } from 'rxjs';

import { toUserMessage } from '../interceptors/error.interceptor';
import { TicketAttachment } from '../models/attachment.models';
import { skipErrorToast } from './http-context';
import { NotificationService } from './notification.service';
import { TicketsApiService } from './tickets-api.service';

/** 404 is the same for a missing file and one outside the user's scope (the server does not say which). */
export const DOWNLOAD_NOT_FOUND_MESSAGE = 'הקובץ לא נמצא או שאין לך גישה אליו';

/**
 * Downloads an attachment through HttpClient – a plain link would not send the Bearer token (spec 6.3: download only
 * through the API, with a permission check). The file is saved under the name the server sends, never opened in the page.
 */
@Injectable({ providedIn: 'root' })
export class AttachmentDownloadService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(TicketsApiService);
  private readonly notifications = inject(NotificationService);
  private readonly document = inject(DOCUMENT);

  /** Completes when the download has started or failed (the error is shown here, as a toast). */
  download(ticketId: number, attachment: TicketAttachment): Observable<void> {
    return this.http
      .get(this.api.attachmentUrl(ticketId, attachment.id), {
        responseType: 'blob',
        observe: 'response',
        context: skipErrorToast(),
      })
      .pipe(
        map((response) => {
          const name = fileNameFromContentDisposition(response.headers.get('Content-Disposition')) ?? attachment.fileName;
          this.save(response.body ?? new Blob(), name);
        }),
        catchError((error: unknown) => {
          // The error body is a Blob (responseType), so the ProblemDetails title is not read.
          const status = error instanceof HttpErrorResponse ? error.status : -1;
          this.notifications.error(
            status === 404 ? DOWNLOAD_NOT_FOUND_MESSAGE : toUserMessage(toPlainError(error)),
          );
          return EMPTY;
        }),
      );
  }

  /** A temporary <a download>; the object URL is released right after the click. */
  private save(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = this.document.createElement('a');
    try {
      link.href = url;
      link.download = fileName;
      link.rel = 'noopener';
      link.style.display = 'none';
      this.document.body.appendChild(link);
      link.click();
    } finally {
      link.remove();
      URL.revokeObjectURL(url);
    }
  }
}

function toPlainError(error: unknown): HttpErrorResponse {
  const status = error instanceof HttpErrorResponse ? error.status : 500;
  return new HttpErrorResponse({ status });
}

/**
 * The file name of a Content-Disposition header (RFC 6266): filename* (UTF-8, percent-encoded – the Hebrew name)
 * first, then filename. Null when there is neither.
 */
export function fileNameFromContentDisposition(header: string | null | undefined): string | null {
  if (!header) {
    return null;
  }
  const extended = /filename\*\s*=\s*([^']*)'[^']*'([^;]+)/i.exec(header);
  if (extended) {
    const charset = extended[1].trim().toLowerCase();
    const value = unquote(extended[2].trim());
    if (charset === '' || charset === 'utf-8') {
      try {
        const decoded = decodeURIComponent(value);
        if (decoded) {
          return decoded;
        }
      } catch {
        // Malformed encoding: fall back to filename.
      }
    }
  }
  const plain = /(?:^|;)\s*filename\s*=\s*("((?:\\.|[^"\\])*)"|[^;]+)/i.exec(header);
  if (plain) {
    const value = plain[2] !== undefined ? plain[2].replace(/\\(.)/g, '$1') : plain[1].trim();
    return value || null;
  }
  return null;
}

function unquote(value: string): string {
  return value.length >= 2 && value.startsWith('"') && value.endsWith('"') ? value.slice(1, -1) : value;
}
