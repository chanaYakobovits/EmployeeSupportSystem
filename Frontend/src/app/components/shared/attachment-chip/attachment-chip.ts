import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { ATTACHMENT_ICONS, TicketAttachment, attachmentKind } from '../../../models/attachment.models';
import { FileSizePipe, formatFileSize } from '../../../pipes/file-size.pipe';
import { AttachmentDownloadService } from '../../../services/attachment-download.service';

/** An attached file: icon, name and size; a click downloads it (through the API, with the token). */
@Component({
  selector: 'app-attachment-chip',
  imports: [MatIconModule, MatProgressSpinnerModule, FileSizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './attachment-chip.html',
  styleUrl: './attachment-chip.scss',
})
export class AttachmentChip {
  readonly ticketId = input.required<number>();
  readonly attachment = input.required<TicketAttachment>();

  private readonly downloads = inject(AttachmentDownloadService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly downloading = signal(false);
  protected readonly icon = computed(() => ATTACHMENT_ICONS[attachmentKind(this.attachment().contentType)]);
  protected readonly ariaLabel = computed(
    () => `הורדת הקובץ ${this.attachment().fileName}, ${formatFileSize(this.attachment().sizeBytes)}`,
  );

  protected download(): void {
    if (this.downloading()) {
      return;
    }
    this.downloading.set(true);
    this.downloads
      .download(this.ticketId(), this.attachment())
      .pipe(
        finalize(() => this.downloading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }
}
