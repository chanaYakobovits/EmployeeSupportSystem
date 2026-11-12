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
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { Department } from '../../../../core/models/department.models';
import { UserSummary, fullName } from '../../../../core/models/user.models';
import { FormAlert, FormAlertBox } from '../../../../shared/components/form-alert/form-alert';
import { applyServerErrors } from '../../../../shared/forms/server-errors';
import { adminAlert } from '../../admin-errors';
import { DepartmentDialogStore } from './department-dialog.store';

export interface DepartmentDialogData {
  /** null → a new department. */
  department: Department | null;
}

/** The manager field holds the chosen user, or the text being typed until one is chosen. */
type ManagerValue = UserSummary | string | null;

/** Text that was typed but not chosen from the list. Empty text = no manager. */
function managerChosen(control: AbstractControl<ManagerValue>): ValidationErrors | null {
  const value = control.value;
  return typeof value === 'string' && value.trim() !== '' ? { notChosen: true } : null;
}

export function managerLabel(user: UserSummary): string {
  return `${fullName(user)} (${user.employeeCode})`;
}

/**
 * Create / edit a department (spec 7.8): name + manager among the active employees, or no manager.
 * Appointing a manager gives them the DepartmentManager role; the previous manager loses it (CLAUDE.md).
 * Closes with the saved department.
 */
@Component({
  selector: 'app-department-dialog',
  imports: [
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    FormAlertBox,
  ],
  providers: [DepartmentDialogStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './department-dialog.html',
  styleUrl: './department-dialog.scss',
})
export class DepartmentDialog {
  private readonly data = inject<DepartmentDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<DepartmentDialog, Department>>(MatDialogRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly store = inject(DepartmentDialogStore);

  protected readonly department = this.data.department;
  protected readonly isEdit = this.department !== null;
  protected readonly alert = signal<FormAlert | null>(null);

  protected readonly form = new FormGroup({
    name: new FormControl(this.department?.name ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100), (c: AbstractControl<string>) => (c.value.trim() ? null : { required: true })],
    }),
    manager: new FormControl<ManagerValue>(this.department?.manager ?? null, { validators: [managerChosen] }),
  });

  protected readonly displayManager = (value: ManagerValue): string =>
    typeof value === 'string' ? value : value ? managerLabel(value) : '';

  protected readonly managerLabel = managerLabel;

  constructor() {
    this.form.controls.manager.valueChanges.pipe(takeUntilDestroyed()).subscribe((value) => {
      if (typeof value === 'string') {
        this.store.search(value);
      }
    });
    this.store.search('');
  }

  protected clearManager(): void {
    this.form.controls.manager.setValue(null);
  }

  protected submit(): void {
    if (this.store.saving()) {
      return;
    }
    this.alert.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focus(this.form.controls.name.invalid ? 'name' : 'manager');
      return;
    }

    const { name, manager } = this.form.getRawValue();
    const managerUserId = manager && typeof manager === 'object' ? manager.id : null;
    const request$ = this.department
      ? this.store.update(this.department.id, { name: name.trim(), managerUserId, isActive: this.department.isActive })
      : this.store.create({ name: name.trim(), managerUserId });

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (saved) => this.dialogRef.close(saved),
      error: (error: unknown) => this.showError(error),
    });
  }

  /**
   * Name taken (409) → next to the name. Manager inactive (400) or already managing another department (422) →
   * next to the manager field.
   */
  private showError(error: unknown): void {
    const applied = applyServerErrors(this.form, error, {
      fieldAliases: { managerUserId: 'manager' },
      typeFields: { 'manages-other-department': 'manager' },
    });
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

export function openDepartmentDialog(dialog: MatDialog, department: Department | null): MatDialogRef<DepartmentDialog, Department> {
  return dialog.open<DepartmentDialog, DepartmentDialogData, Department>(DepartmentDialog, {
    data: { department },
    width: '520px',
    maxWidth: 'calc(100vw - 32px)',
    ariaLabelledBy: 'department-dialog-title',
  });
}
