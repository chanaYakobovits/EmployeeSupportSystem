import { Clipboard } from '@angular/cdk/clipboard';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

export interface TemporaryPasswordData {
  employeeCode: string;
  fullName: string;
  /** Cleared as soon as the dialog has read it – the only copy is the dialog's, until it closes. */
  temporaryPassword: string;
  reason: 'created' | 'reset';
}

type CopyStatus = 'idle' | 'copied' | 'failed';

/**
 * Shows a temporary password once (spec 4.2): employee code + password, a copy button and a warning.
 * Closes only with "העתקתי, סגור" – never with Esc or a click outside, so it is not dismissed by accident.
 * The password is not kept after closing and is never logged.
 */
@Component({
  selector: 'app-temp-password-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './temp-password-dialog.html',
  styleUrl: './temp-password-dialog.scss',
})
export class TemporaryPasswordDialog {
  private readonly dialogRef = inject<MatDialogRef<TemporaryPasswordDialog, void>>(MatDialogRef);
  private readonly clipboard = inject(Clipboard);
  private readonly data = inject<TemporaryPasswordData>(MAT_DIALOG_DATA);

  protected readonly employeeCode = this.data.employeeCode;
  protected readonly fullName = this.data.fullName;
  protected readonly reason = this.data.reason;
  protected readonly password = signal(this.data.temporaryPassword);
  protected readonly copyStatus = signal<CopyStatus>('idle');

  constructor() {
    // Enforced here, whoever opens the dialog.
    this.dialogRef.disableClose = true;
    this.data.temporaryPassword = '';
  }

  protected copy(): void {
    const copied = this.clipboard.copy(`קוד עובד: ${this.employeeCode}\nסיסמה זמנית: ${this.password()}`);
    this.copyStatus.set(copied ? 'copied' : 'failed');
  }

  protected close(): void {
    this.password.set('');
    this.dialogRef.close();
  }
}

/** Opens the dialog; afterClosed() emits when the admin confirmed they copied the details. */
export function openTemporaryPasswordDialog(
  dialog: MatDialog,
  data: TemporaryPasswordData,
): MatDialogRef<TemporaryPasswordDialog, void> {
  return dialog.open<TemporaryPasswordDialog, TemporaryPasswordData, void>(TemporaryPasswordDialog, {
    data,
    disableClose: true,
    role: 'alertdialog',
    width: '480px',
    maxWidth: 'calc(100vw - 32px)',
    ariaLabelledBy: 'temp-password-title',
  });
}
