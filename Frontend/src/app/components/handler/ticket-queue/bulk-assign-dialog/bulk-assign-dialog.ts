import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';

import { BulkAssignOptions, BulkUpdateResult, REASON_LIMITS } from '../../../../models/ticket.models';
import { applyServerErrors } from '../../../../services/server-errors';
import { TicketQueueStore } from '../../../../services/ticket-queue.store';
import { FormAlert, FormAlertBox } from '../../../shared/form-alert/form-alert';
import { LoadingState } from '../../../shared/loading-state/loading-state';
import { changeErrorAlert } from '../../ticket-handle/change-errors';
import { reasonValidator } from '../../ticket-handle/reclassify-dialog/reclassify-dialog';
import { AssignTarget } from '../../ticket-handle/assign-dialog/assign-dialog';
import { ticketCount } from '../../handler-messages';

type OptionsState = { status: 'loading' } | { status: 'error' } | { status: 'ready'; options: BulkAssignOptions };

/**
 * "העברה" of the selected tickets (task 3.6): to a handler of the user's department or another department's queue, with
 * one required reason for all of them. The choices come from GET /bulk/assign-options; what depends on one ticket (its
 * requester, its handler) is checked per ticket by the server. Closes with the result; a 400 stays open on the field.
 */
@Component({
  selector: 'app-bulk-assign-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatRadioModule,
    MatSelectModule,
    FormAlertBox,
    LoadingState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bulk-assign-dialog.html',
  styleUrl: './bulk-assign-dialog.scss',
})
export class BulkAssignDialog {
  protected readonly store = inject(TicketQueueStore);
  private readonly dialogRef = inject<MatDialogRef<BulkAssignDialog, BulkUpdateResult>>(MatDialogRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly reasonLimits = REASON_LIMITS;
  protected readonly heading = `העברת ${ticketCount(this.store.selectedCount())}`;
  protected readonly optionsState = signal<OptionsState>({ status: 'loading' });
  protected readonly alert = signal<FormAlert | null>(null);

  protected readonly form = new FormGroup({
    target: new FormControl<AssignTarget>('handler', { nonNullable: true }),
    userId: new FormControl<number | null>(null, { validators: [Validators.required] }),
    departmentId: new FormControl<number | null>({ value: null, disabled: true }, { validators: [Validators.required] }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [reasonValidator, Validators.maxLength(REASON_LIMITS.maxLength)],
    }),
  });

  protected readonly target = toSignal(this.form.controls.target.valueChanges, { initialValue: 'handler' as AssignTarget });
  protected readonly options = computed(() => {
    const state = this.optionsState();
    return state.status === 'ready' ? state.options : null;
  });

  constructor() {
    this.form.controls.target.valueChanges.pipe(takeUntilDestroyed()).subscribe((target) => this.syncTarget(target));
    this.loadOptions();
  }

  protected loadOptions(): void {
    this.optionsState.set({ status: 'loading' });
    this.store
      .bulkAssignOptions()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (options) => this.optionsState.set({ status: 'ready', options }),
        error: () => this.optionsState.set({ status: 'error' }),
      });
  }

  protected submit(): void {
    if (this.store.bulkBusy() || this.options() === null) {
      return;
    }
    this.alert.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      const field = this.form.controls.target.value === 'handler' ? 'userId' : 'departmentId';
      this.focus(this.form.controls[field].invalid ? field : 'reason');
      return;
    }

    const { target, userId, departmentId, reason } = this.form.getRawValue();
    this.store
      .bulkAssign(target === 'handler' ? { targetUserId: userId! } : { targetDepartmentId: departmentId! }, reason.trim())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => this.dialogRef.close(result),
        error: (error: unknown) => this.showError(error),
      });
  }

  /** Only the field of the chosen target is enabled (and required). */
  private syncTarget(target: AssignTarget): void {
    const [on, off] =
      target === 'handler'
        ? [this.form.controls.userId, this.form.controls.departmentId]
        : [this.form.controls.departmentId, this.form.controls.userId];
    on.enable({ emitEvent: false });
    off.reset(null, { emitEvent: false });
    off.disable({ emitEvent: false });
  }

  private showError(error: unknown): void {
    const applied = applyServerErrors(this.form, error, {
      fieldAliases: { targetUserId: 'userId', targetDepartmentId: 'departmentId' },
    });
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

/** Opened with the queue page's injector, so the dialog shares its TicketQueueStore (the selection). */
export function openBulkAssignDialog(
  dialog: MatDialog,
  injector: Injector,
): MatDialogRef<BulkAssignDialog, BulkUpdateResult> {
  return dialog.open<BulkAssignDialog, void, BulkUpdateResult>(BulkAssignDialog, {
    injector,
    width: '520px',
    maxWidth: 'calc(100vw - 32px)',
    ariaLabelledBy: 'bulk-assign-dialog-title',
  });
}
