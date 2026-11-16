import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';

import { Category } from '../../../../models/category.models';
import { URGENCIES, Urgency } from '../../../../models/sla.models';
import { REASON_LIMITS } from '../../../../models/ticket.models';
import { UrgencyLabelPipe } from '../../../../pipes/urgency-label.pipe';
import { applyServerErrors } from '../../../../services/server-errors';
import { ChangeOutcome, TicketHandleStore } from '../../../../services/ticket-handle.store';
import { FormAlert, FormAlertBox } from '../../../shared/form-alert/form-alert';
import { changeErrorAlert } from '../change-errors';

/** Not empty after trimming, and within the server's length (TicketFieldLimits.Reason*). */
export function reasonValidator(control: AbstractControl<string>): ValidationErrors | null {
  const length = control.value.trim().length;
  if (length === 0) {
    return { required: true };
  }
  return length < REASON_LIMITS.minLength ? { minlength: true } : null;
}

export const UNCHANGED_MESSAGE = 'הקטגוריה ורמת הדחיפות לא השתנו';

/**
 * "שינוי סיווג" (spec 4.4): category, urgency and a required reason (shown in the staff timeline only).
 * Closes with the outcome. On 409 it stays open with the message: the store has reloaded the ticket, the reason the user
 * wrote is kept, and the next try sends the new version.
 */
@Component({
  selector: 'app-reclassify-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    FormAlertBox,
    UrgencyLabelPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reclassify-dialog.html',
  styleUrl: './reclassify-dialog.scss',
})
export class ReclassifyDialog {
  protected readonly store = inject(TicketHandleStore);
  private readonly dialogRef = inject<MatDialogRef<ReclassifyDialog, ChangeOutcome>>(MatDialogRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly urgencies = URGENCIES;
  protected readonly reasonLimits = REASON_LIMITS;
  protected readonly categories = signal<Category[] | null>(null);
  protected readonly categoriesFailed = signal(false);
  protected readonly alert = signal<FormAlert | null>(null);

  private readonly initial = this.store.ticket();

  protected readonly form = new FormGroup({
    category: new FormControl<number | null>(this.initial?.category?.id ?? null, { validators: [Validators.required] }),
    urgency: new FormControl<Urgency | null>(this.initial?.urgency ?? null),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [reasonValidator, Validators.maxLength(REASON_LIMITS.maxLength)],
    }),
  });

  constructor() {
    this.store
      .activeCategories()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (categories) => {
          this.categories.set(categories);
          // An inactive current category cannot be kept: the user picks another one.
          const current = this.form.controls.category.value;
          if (current !== null && !categories.some((c) => c.id === current)) {
            this.form.controls.category.reset(null);
          }
        },
        error: () => this.categoriesFailed.set(true),
      });
  }

  protected submit(): void {
    if (this.store.busy()) {
      return;
    }
    this.alert.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focus(this.form.controls.category.invalid ? 'category' : 'reason');
      return;
    }

    const ticket = this.store.ticket();
    const { category, urgency, reason } = this.form.getRawValue();
    // Same values are accepted by the server only to confirm an automatic / fallback classification.
    if (ticket && ticket.classificationSource === 'Manual' && category === ticket.category?.id && urgency === ticket.urgency) {
      this.alert.set({ title: UNCHANGED_MESSAGE });
      return;
    }

    this.store
      .reclassify({ categoryId: category!, urgency: urgency ?? undefined, reason: reason.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (outcome) => this.dialogRef.close(outcome),
        error: (error: unknown) => this.showError(error),
      });
  }

  private showError(error: unknown): void {
    const applied = applyServerErrors(this.form, error, { fieldAliases: { categoryId: 'category' } });
    if (applied.fields.length > 0 && applied.unmatched.length === 0) {
      this.focus(applied.fields[0]);
      return;
    }
    this.alert.set(changeErrorAlert(error, applied.unmatched));
  }

  private focus(name: string): void {
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLElement>(`[formControlName="${name}"]`)?.focus(),
      { injector: this.injector },
    );
  }
}

/** Opened with the page's injector, so the dialog shares the page's TicketHandleStore. */
export function openReclassifyDialog(
  dialog: MatDialog,
  injector: Injector,
): MatDialogRef<ReclassifyDialog, ChangeOutcome> {
  return dialog.open<ReclassifyDialog, void, ChangeOutcome>(ReclassifyDialog, {
    injector,
    width: '520px',
    maxWidth: 'calc(100vw - 32px)',
    ariaLabelledBy: 'reclassify-dialog-title',
  });
}
