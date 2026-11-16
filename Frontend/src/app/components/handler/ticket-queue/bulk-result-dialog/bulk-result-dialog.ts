import { LiveAnnouncer } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { BulkAction, BulkUpdateResult } from '../../../../models/ticket.models';
import { bulkFailureText, bulkResultSummary } from '../../handler-messages';
import { HandlerNavigationState, handlerTicketUrl } from '../../handler-urls';

export interface BulkResultDialogData {
  action: BulkAction;
  result: BulkUpdateResult;
  /** The queue's URL, so the handling screen of a failed ticket can return to the same view. */
  queueUrl: string;
}

/**
 * The result of a bulk action (spec 11.2, task 3.6): how many were updated and, for each ticket that was not, its
 * number (a link to the handling screen) and the reason in Hebrew. The summary is also announced to screen readers.
 */
@Component({
  selector: 'app-bulk-result-dialog',
  imports: [RouterLink, MatButtonModule, MatDialogModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bulk-result-dialog.html',
  styleUrl: './bulk-result-dialog.scss',
})
export class BulkResultDialog {
  protected readonly data = inject<BulkResultDialogData>(MAT_DIALOG_DATA);

  protected readonly summary = bulkResultSummary(this.data.result.succeeded.length, this.data.result.failed.length);
  protected readonly failures = this.data.result.failed.map((failure) => ({
    id: failure.id,
    text: bulkFailureText(failure, this.data.action),
  }));
  protected readonly ticketUrl = handlerTicketUrl;
  protected readonly navigationState: HandlerNavigationState = { queueUrl: this.data.queueUrl };

  constructor() {
    const announcement =
      this.failures.length > 0 ? `${this.summary}. הפניות שלא עודכנו נשארו מסומנות ברשימה.` : this.summary;
    void inject(LiveAnnouncer).announce(announcement, 'polite');
  }
}

export function openBulkResultDialog(
  dialog: MatDialog,
  data: BulkResultDialogData,
): MatDialogRef<BulkResultDialog, void> {
  return dialog.open<BulkResultDialog, BulkResultDialogData, void>(BulkResultDialog, {
    data,
    width: '520px',
    maxWidth: 'calc(100vw - 32px)',
    ariaLabelledBy: 'bulk-result-title',
    ariaDescribedBy: 'bulk-result-summary',
  });
}
