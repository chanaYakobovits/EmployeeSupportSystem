import { HttpErrorResponse } from '@angular/common/http';
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
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { debounceTime, map } from 'rxjs';

import { toUserMessage } from '../../../../interceptors/error.interceptor';
import { isProblemDetails } from '../../../../models/problem-details';
import { COMMENT_FIELD_LIMITS } from '../../../../models/ticket.models';
import { applyServerErrors } from '../../../../services/server-errors';
import { TicketViewStore } from '../../../../services/ticket-view.store';
import { FormAlert, FormAlertBox } from '../../../shared/form-alert/form-alert';
import { COUNTER_ANNOUNCE_DELAY_MS, describeCount, formatCount } from '../../character-count';
import { EMPLOYEE_CONFLICT_MESSAGE } from '../ticket-view-messages';

const { bodyMaxLength } = COMMENT_FIELD_LIMITS;

/**
 * "הבעיה לא נפתרה – פתיחה מחדש" (spec 3.3): Resolved → InProgress, with an optional message to the handler (a visible
 * comment saved with the change). Closes with true when reopened. On 409 / 422 it stays open with the message and the
 * text; the store has reloaded the ticket, so the next try sends the new version.
 */
@Component({
  selector: 'app-reopen-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    FormAlertBox,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reopen-dialog.html',
  styleUrl: './reopen-dialog.scss',
})
export class ReopenDialog {
  protected readonly store = inject(TicketViewStore);
  private readonly dialogRef = inject<MatDialogRef<ReopenDialog, boolean>>(MatDialogRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly messageField = viewChild.required<ElementRef<HTMLTextAreaElement>>('messageField');

  protected readonly bodyMaxLength = bodyMaxLength;
  protected readonly bodyMaxText = formatCount(bodyMaxLength);
  protected readonly alert = signal<FormAlert | null>(null);

  protected readonly form = new FormGroup({
    message: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(bodyMaxLength)] }),
  });

  private readonly messageValue = toSignal(this.form.controls.message.valueChanges, { initialValue: '' });
  protected readonly messageLength = computed(() => this.messageValue().length);
  protected readonly counterAnnouncement = toSignal(
    this.form.controls.message.valueChanges.pipe(
      debounceTime(COUNTER_ANNOUNCE_DELAY_MS),
      map((value) => describeCount(value, { max: bodyMaxLength })),
    ),
    { initialValue: '' },
  );

  protected submit(): void {
    if (this.store.sending()) {
      return;
    }
    this.alert.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusMessage();
      return;
    }
    const message = this.form.controls.message.value.trim();
    this.store
      .changeStatus('InProgress', message || undefined)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.dialogRef.close(true),
        error: (error: unknown) => this.showError(error),
      });
  }

  private showError(error: unknown): void {
    const applied = applyServerErrors(this.form, error);
    if (applied.fields.length > 0 && applied.unmatched.length === 0) {
      this.focusMessage();
      return;
    }
    const httpError = error instanceof HttpErrorResponse ? error : new HttpErrorResponse({ status: 500 });
    if (httpError.status === 409) {
      this.alert.set({ title: EMPLOYEE_CONFLICT_MESSAGE, detail: 'מה שכתבת נשמר בחלון.' });
      return;
    }
    // 422 (e.g. the 7 days have passed): the server's Hebrew title.
    const alert: FormAlert = { title: toUserMessage(httpError) };
    if (applied.unmatched.length > 0) {
      alert.detail = applied.unmatched.join(' ');
    } else if (isProblemDetails(httpError.error) && httpError.error.detail) {
      alert.detail = httpError.error.detail;
    }
    this.alert.set(alert);
  }

  private focusMessage(): void {
    afterNextRender(() => this.messageField().nativeElement.focus(), { injector: this.injector });
  }
}

/** Opened with the page's injector, so the dialog shares the page's TicketViewStore. Closes with true when reopened. */
export function openReopenDialog(dialog: MatDialog, injector: Injector): MatDialogRef<ReopenDialog, boolean> {
  return dialog.open<ReopenDialog, void, boolean>(ReopenDialog, {
    injector,
    width: '520px',
    maxWidth: 'calc(100vw - 32px)',
    ariaLabelledBy: 'reopen-dialog-title',
    ariaDescribedBy: 'reopen-dialog-description',
  });
}
