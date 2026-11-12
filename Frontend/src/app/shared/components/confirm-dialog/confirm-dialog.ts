import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

export interface ConfirmDialogData {
  heading: string;
  /** What is about to happen. */
  message: string;
  /** The consequences, one per bullet. */
  consequences?: readonly string[];
  confirmText: string;
  cancelText?: string;
  /** danger: an action that is hard to undo (deactivate, reset password). */
  tone?: 'primary' | 'danger';
}

/**
 * Confirmation before a sensitive action. Open it through ConfirmService.
 * Closes with true (confirm) or false / undefined (cancel, Esc, click outside). Focus starts on "cancel".
 */
@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirm-dialog.html',
  styleUrl: './confirm-dialog.scss',
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
  protected readonly tone = this.data.tone ?? 'primary';
}
