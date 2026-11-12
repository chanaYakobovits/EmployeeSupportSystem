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
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';

import {
  CATEGORY_CODE_PATTERN,
  CATEGORY_LIMITS,
  Category,
  CategorySaveResult,
  ROUTING_TYPES,
  RoutingType,
} from '../../../../core/models/category.models';
import { DepartmentSummary } from '../../../../core/models/department.models';
import { FormAlert, FormAlertBox } from '../../../../shared/components/form-alert/form-alert';
import { applyServerErrors } from '../../../../shared/forms/server-errors';
import { RoutingTypeLabelPipe } from '../../../../shared/pipes/routing-type-label.pipe';
import { adminAlert } from '../../admin-errors';
import { CategoryDialogStore } from './category-dialog.store';

export interface CategoryDialogData {
  /** null → a new category. */
  category: Category | null;
}

/** Not empty after trimming (Validators.required accepts spaces). */
function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim() ? null : { required: true };
}

/** Only routing to a fixed department needs a handling department (spec 3.5). */
export function needsHandlingDepartment(routingType: RoutingType | null): boolean {
  return routingType === 'FixedDepartment';
}

/**
 * Create / edit a category (spec 7.8): code (fixed after creation), name, description (sent to the AI),
 * routing type and – for FixedDepartment only – the handling department. Closes with the save result,
 * whose warnings the screen shows.
 */
@Component({
  selector: 'app-category-dialog',
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
    RoutingTypeLabelPipe,
  ],
  providers: [CategoryDialogStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './category-dialog.html',
  styleUrl: './category-dialog.scss',
})
export class CategoryDialog {
  private readonly data = inject<CategoryDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<CategoryDialog, CategorySaveResult>>(MatDialogRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly store = inject(CategoryDialogStore);

  protected readonly category = this.data.category;
  protected readonly isEdit = this.category !== null;
  protected readonly limits = CATEGORY_LIMITS;
  protected readonly routingTypes = ROUTING_TYPES;
  protected readonly alert = signal<FormAlert | null>(null);

  protected readonly form = new FormGroup({
    code: new FormControl(
      { value: this.category?.code ?? '', disabled: this.isEdit },
      {
        nonNullable: true,
        validators: [Validators.required, Validators.pattern(CATEGORY_CODE_PATTERN)],
      },
    ),
    name: new FormControl(this.category?.name ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(CATEGORY_LIMITS.nameMaxLength), notBlank],
    }),
    description: new FormControl(this.category?.description ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(CATEGORY_LIMITS.descriptionMaxLength), notBlank],
    }),
    routingType: new FormControl<RoutingType | null>(this.category?.routingType ?? null, {
      validators: [Validators.required],
    }),
    handlingDepartment: new FormControl<number | null>(this.category?.handlingDepartment?.id ?? null, {
      validators: [Validators.required],
    }),
  });

  private readonly routingType = toSignal(this.form.controls.routingType.valueChanges, {
    initialValue: this.form.controls.routingType.value,
  });
  protected readonly showsHandlingDepartment = computed(() => needsHandlingDepartment(this.routingType()));

  private readonly description = toSignal(this.form.controls.description.valueChanges, {
    initialValue: this.form.controls.description.value,
  });
  protected readonly descriptionLength = computed(() => this.description().length);

  /**
   * Active departments, plus the current one when it is no longer active – so the field shows it
   * (saving an active category with it is refused by the server).
   */
  protected readonly departmentOptions = computed<(DepartmentSummary & { inactive?: boolean })[]>(() => {
    const options = this.store.departments();
    const current = this.category?.handlingDepartment;
    return current && !options.some((d) => d.id === current.id) ? [...options, { ...current, inactive: true }] : options;
  });

  constructor() {
    this.syncHandlingDepartment(this.form.controls.routingType.value);
    this.form.controls.routingType.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((routingType) => this.syncHandlingDepartment(routingType));
    this.store.loadDepartments();
  }

  /** Codes are upper case: what is typed is converted on the fly. */
  protected upperCaseCode(): void {
    const control = this.form.controls.code;
    const upper = control.value.toUpperCase();
    if (upper !== control.value) {
      control.setValue(upper);
    }
  }

  protected submit(): void {
    if (this.store.saving()) {
      return;
    }
    this.alert.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      const firstInvalid = Object.keys(this.form.controls).find(
        (name) => this.form.get(name)!.enabled && this.form.get(name)!.invalid,
      );
      this.focus(firstInvalid ?? 'name');
      return;
    }

    const { code, name, description, routingType, handlingDepartment } = this.form.getRawValue();
    const fields = {
      name: name.trim(),
      description: description.trim(),
      routingType: routingType!,
      handlingDepartmentId: needsHandlingDepartment(routingType) ? handlingDepartment : null,
    };
    const request$ = this.category
      ? this.store.update(this.category.id, { ...fields, isActive: this.category.isActive })
      : this.store.create({ code: code.trim(), ...fields });

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => this.dialogRef.close(result),
      error: (error: unknown) => this.showError(error),
    });
  }

  /** The handling department is enabled (and required) only for FixedDepartment; otherwise it is cleared. */
  private syncHandlingDepartment(routingType: RoutingType | null): void {
    const control = this.form.controls.handlingDepartment;
    if (needsHandlingDepartment(routingType)) {
      control.enable({ emitEvent: false });
    } else {
      control.reset(null, { emitEvent: false });
      control.disable({ emitEvent: false });
    }
  }

  /** Code taken (409), invalid field (400) → next to the field; anything else → an alert at the top. */
  private showError(error: unknown): void {
    const applied = applyServerErrors(this.form, error, {
      fieldAliases: { handlingDepartmentId: 'handlingDepartment' },
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

export function openCategoryDialog(
  dialog: MatDialog,
  category: Category | null,
): MatDialogRef<CategoryDialog, CategorySaveResult> {
  return dialog.open<CategoryDialog, CategoryDialogData, CategorySaveResult>(CategoryDialog, {
    data: { category },
    width: '600px',
    maxWidth: 'calc(100vw - 32px)',
    ariaLabelledBy: 'category-dialog-title',
  });
}
