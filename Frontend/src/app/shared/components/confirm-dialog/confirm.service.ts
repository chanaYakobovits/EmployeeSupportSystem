import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';

import { ConfirmDialog, ConfirmDialogData } from './confirm-dialog';

/** Asks the user to confirm a sensitive action. Emits true only when confirmed. */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  confirm(data: ConfirmDialogData): Observable<boolean> {
    return this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
        data,
        role: 'alertdialog',
        width: '480px',
        maxWidth: 'calc(100vw - 32px)',
        ariaLabelledBy: 'confirm-dialog-title',
        ariaDescribedBy: 'confirm-dialog-content',
      })
      .afterClosed()
      .pipe(map((confirmed) => confirmed === true));
  }
}
