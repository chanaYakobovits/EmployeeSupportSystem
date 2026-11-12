import { EnvironmentInjector, Injectable, inject } from '@angular/core';
import type { MatSnackBar } from '@angular/material/snack-bar';

/**
 * Single entry point for transient user notifications (toasts).
 * MatSnackBar (and the CDK overlay it pulls in) is loaded lazily, so it stays out of the initial bundle
 * (CLAUDE.md: under 500KB). The chunk is fetched as soon as the service is created – at startup, by the
 * error interceptor – so a toast is ready even if the network goes down later.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly injector = inject(EnvironmentInjector);
  private readonly snackBar: Promise<MatSnackBar> = import('@angular/material/snack-bar').then((m) =>
    this.injector.get(m.MatSnackBar),
  );

  error(message: string): void {
    void this.snackBar.then((snackBar) =>
      snackBar.open(message, 'סגירה', {
        duration: 6000,
        panelClass: 'notification-error',
        politeness: 'assertive',
      }),
    );
  }

  success(message: string): void {
    void this.snackBar.then((snackBar) => snackBar.open(message, 'סגירה', { duration: 4000 }));
  }
}
