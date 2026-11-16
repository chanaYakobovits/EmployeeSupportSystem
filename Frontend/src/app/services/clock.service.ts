import { DestroyRef, Injectable, NgZone, inject, signal } from '@angular/core';

/** How often the countdowns move. */
export const CLOCK_TICK_MS = 60_000;

/**
 * The current time as a signal that moves once a minute – for countdowns to the SLA due time (task 3.4), so they and the
 * overdue marks update without reloading the list. One timer for the whole app.
 * The timer runs outside the Angular zone, so it never keeps the app "unstable" (tests, whenStable); setting the signal
 * still schedules change detection for the views that read it.
 */
@Injectable({ providedIn: 'root' })
export class ClockService {
  private readonly nowSignal = signal(new Date());
  readonly now = this.nowSignal.asReadonly();

  constructor() {
    const timer = inject(NgZone).runOutsideAngular(() =>
      setInterval(() => this.nowSignal.set(new Date()), CLOCK_TICK_MS),
    );
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }
}
