import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/** Single entry point for transient user notifications (toasts). */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  error(message: string): void {
    this.snackBar.open(message, 'סגירה', {
      duration: 6000,
      panelClass: 'notification-error',
      politeness: 'assertive',
    });
  }

  success(message: string): void {
    this.snackBar.open(message, 'סגירה', { duration: 4000 });
  }
}
