import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { SLA_MAX_HOURS, SlaField, SlaPolicies } from '../../../../core/models/sla.models';
import { NotificationService } from '../../../../core/notifications/notification.service';
import { ErrorState } from '../../../../shared/components/error-state/error-state';
import { FormAlert, FormAlertBox } from '../../../../shared/components/form-alert/form-alert';
import { LoadingState } from '../../../../shared/components/loading-state/loading-state';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { UrgencyBadge } from '../../../../shared/components/urgency-badge/urgency-badge';
import { applyServerErrors } from '../../../../shared/forms/server-errors';
import { adminAlert } from '../../admin-errors';
import { SLA_LEVELS, SLA_ORDER_ERROR, formatHours, slaOrderValidator } from '../sla-rules';
import { SlaStore } from '../sla.store';

/** Whole hours (the number input also accepts fractions). */
function wholeNumber(control: AbstractControl<number | null>): ValidationErrors | null {
  const value = control.value;
  return value === null || Number.isInteger(value) ? null : { wholeNumber: true };
}

function hoursControl(): FormControl<number | null> {
  return new FormControl<number | null>(null, {
    validators: [Validators.required, Validators.min(1), Validators.max(SLA_MAX_HOURS), wholeNumber],
  });
}

/**
 * SLA targets (spec 3.4, 7.8): one form with the four urgency levels, in whole calendar hours, with a readable
 * duration next to each. Critical ≤ High ≤ Medium ≤ Low. Applies to new tickets only.
 */
@Component({
  selector: 'app-sla-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    PageHeader,
    LoadingState,
    ErrorState,
    FormAlertBox,
    UrgencyBadge,
  ],
  providers: [SlaStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sla-page.html',
  styleUrl: './sla-page.scss',
})
export class SlaPage {
  protected readonly store = inject(SlaStore);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly levels = SLA_LEVELS;
  protected readonly maxHours = SLA_MAX_HOURS;
  protected readonly orderError = SLA_ORDER_ERROR;
  protected readonly alert = signal<FormAlert | null>(null);

  protected readonly form = new FormGroup(
    {
      critical: hoursControl(),
      high: hoursControl(),
      medium: hoursControl(),
      low: hoursControl(),
    },
    { validators: [slaOrderValidator] },
  );

  private readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.value });

  constructor() {
    // Fill the form whenever the server's values arrive (load, or after a save).
    effect(() => {
      const policies = this.store.policies();
      if (policies) {
        this.form.reset(policies);
      }
    });
    this.store.load();
  }

  /** "72 שעות = 3 ימים" for the current value of a level. */
  protected duration(field: SlaField): string {
    return formatHours(this.values()[field]);
  }

  protected submit(): void {
    if (this.store.saving()) {
      return;
    }
    this.alert.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focus(SLA_LEVELS.find((level) => this.form.controls[level.field].invalid)?.field ?? 'critical');
      return;
    }

    this.store
      .save(this.form.getRawValue() as SlaPolicies)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.notifications.success('יעדי ה-SLA עודכנו'),
        error: (error: unknown) => this.showError(error),
      });
  }

  protected reset(): void {
    const policies = this.store.policies();
    if (policies) {
      this.form.reset(policies);
      this.alert.set(null);
    }
  }

  private showError(error: unknown): void {
    const applied = applyServerErrors(this.form, error);
    if (applied.fields.length > 0 && applied.unmatched.length === 0) {
      this.focus(applied.fields[0]);
      return;
    }
    const alert = adminAlert(error);
    if (applied.unmatched.length > 0) {
      alert.detail = applied.unmatched.join(' ');
    }
    this.alert.set(alert);
  }

  private focus(name: string): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(`[formControlName="${name}"]`)?.focus(), {
      injector: this.injector,
    });
  }
}
