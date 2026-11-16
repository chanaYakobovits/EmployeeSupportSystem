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
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';

import { REASON_LIMITS, TicketAssignOptions } from '../../../../models/ticket.models';
import { applyServerErrors } from '../../../../services/server-errors';
import { ChangeOutcome, TicketHandleStore } from '../../../../services/ticket-handle.store';
import { FormAlert, FormAlertBox } from '../../../shared/form-alert/form-alert';
import { LoadingState } from '../../../shared/loading-state/loading-state';
import { changeErrorAlert } from '../change-errors';
import { reasonValidator } from '../reclassify-dialog/reclassify-dialog';

/** A handler of the handling department, or another department's queue (handlers only). */
export type AssignTarget = 'handler' | 'department';

type OptionsState = { status: 'loading' } | { status: 'error' } | { status: 'ready'; options: TicketAssignOptions };

/**
 * "העברה" (spec 4.4): to another handler of the handling department, or – when the server allows it
 * (transferToDepartment) – to another department's queue; a reason is required (staff timeline only).
 * The choices come from GET /assign-options, by the same rules as POST /assign. 409 → stays open, the reason is kept.
 */
@Component({
  selector: 'app-assign-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatRadioModule,
    MatSelectModule,
    FormAlertBox,
    LoadingState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './assign-dialog.html',
  styleUrl: './assign-dialog.scss',
})
export class AssignDialog {
  protected readonly store = inject(TicketHandleStore);
  private readonly dialogRef = inject<MatDialogRef<AssignDialog, ChangeOutcome>>(MatDialogRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly reasonLimits = REASON_LIMITS;
  /** Another department is offered only when the server allows it. */
  protected readonly canTransfer = this.store.can('transferToDepartment');
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
      .assignOptions()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (options) => this.optionsState.set({ status: 'ready', options }),
        error: () => this.optionsState.set({ status: 'error' }),
      });
  }

  protected submit(): void {
    if (this.store.busy() || this.options() === null) {
      return;
    }
    this.alert.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      const target = this.form.controls.target.value;
      const field = target === 'handler' ? 'userId' : 'departmentId';
      this.focus(this.form.controls[field].invalid ? field : 'reason');
      return;
    }

    const { target, userId, departmentId, reason } = this.form.getRawValue();
    const request =
      target === 'handler'
        ? { targetUserId: userId!, reason: reason.trim() }
        : { targetDepartmentId: departmentId!, reason: reason.trim() };
    this.store
      .assign(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (outcome) => this.dialogRef.close(outcome),
        error: (error: unknown) => {
          // The ticket was reloaded – the choices may have changed (e.g. another handler took it).
          this.loadOptions();
          this.showError(error);
        },
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

/** Opened with the page's injector, so the dialog shares the page's TicketHandleStore. */
export function openAssignDialog(dialog: MatDialog, injector: Injector): MatDialogRef<AssignDialog, ChangeOutcome> {
  return dialog.open<AssignDialog, void, ChangeOutcome>(AssignDialog, {
    injector,
    width: '520px',
    maxWidth: 'calc(100vw - 32px)',
    ariaLabelledBy: 'assign-dialog-title',
  });
}
