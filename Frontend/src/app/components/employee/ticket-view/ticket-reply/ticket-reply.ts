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
  input,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { debounceTime, map } from 'rxjs';

import { toUserMessage } from '../../../../interceptors/error.interceptor';
import { isProblemDetails } from '../../../../models/problem-details';
import { COMMENT_FIELD_LIMITS } from '../../../../models/ticket.models';
import { NotificationService } from '../../../../services/notification.service';
import {
  FILES_FIELD,
  applyServerErrors,
  attachmentServerError,
  clearServerErrors,
} from '../../../../services/server-errors';
import { TicketViewStore, isTicketClosedError } from '../../../../services/ticket-view.store';
import { FileUpload } from '../../../shared/file-upload/file-upload';
import { FormAlert, FormAlertBox } from '../../../shared/form-alert/form-alert';
import { COUNTER_ANNOUNCE_DELAY_MS, describeCount, formatCount } from '../../character-count';

const { bodyMaxLength } = COMMENT_FIELD_LIMITS;

export const REPLY_SENT_MESSAGE = 'התגובה נשלחה';
export const EMPTY_REPLY_MESSAGE = 'יש לכתוב תגובה או לצרף קובץ';

/**
 * The employee's reply at the bottom of the ticket view (spec 7.5): text (up to 4,000) and/or up to 3 files.
 * Text → a comment; files only → attachments. Empty → nothing is sent.
 */
@Component({
  selector: 'app-ticket-reply',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    FileUpload,
    FormAlertBox,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-reply.html',
  styleUrl: './ticket-reply.scss',
})
export class TicketReply {
  /** The handler asked the employee for more information (WaitingForEmployee). */
  readonly waiting = input(false);

  protected readonly store = inject(TicketViewStore);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly bodyField = viewChild.required<ElementRef<HTMLTextAreaElement>>('bodyField');
  private readonly fileUpload = viewChild.required(FileUpload);

  protected readonly bodyMaxLength = bodyMaxLength;
  protected readonly emptyReplyMessage = EMPTY_REPLY_MESSAGE;
  protected readonly bodyMaxText = formatCount(bodyMaxLength);

  protected readonly form = new FormGroup({
    body: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(bodyMaxLength)] }),
    files: new FormControl<File[]>([], { nonNullable: true }),
  });

  protected readonly alert = signal<FormAlert | null>(null);
  protected readonly filesError = signal<string | null>(null);

  private readonly bodyValue = toSignal(this.form.controls.body.valueChanges, { initialValue: '' });
  private readonly filesValue = toSignal(this.form.controls.files.valueChanges, { initialValue: [] as File[] });
  protected readonly bodyLength = computed(() => this.bodyValue().length);

  protected readonly counterAnnouncement = toSignal(
    this.form.controls.body.valueChanges.pipe(
      debounceTime(COUNTER_ANNOUNCE_DELAY_MS),
      map((value) => describeCount(value, { max: bodyMaxLength })),
    ),
    { initialValue: '' },
  );

  constructor() {
    // The server's error about the files is about the files that were sent: a new choice clears it.
    this.form.controls.files.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.filesError.set(null));
  }

  /** Text typed or files chosen – leaving the page asks first. */
  hasText(): boolean {
    return this.bodyValue().trim() !== '';
  }

  hasFiles(): boolean {
    return this.filesValue().length > 0;
  }

  protected submit(): void {
    if (this.store.sending()) {
      return;
    }
    this.alert.set(null);
    this.filesError.set(null);
    clearServerErrors(this.form);

    const body = this.form.controls.body.value.trim();
    const files = this.form.controls.files.value;
    if (body === '' && files.length === 0) {
      this.form.controls.body.setErrors({ required: true });
      this.form.controls.body.markAsTouched();
      this.focusBody();
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusBody();
      return;
    }

    this.store
      .sendReply(body, files)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.form.reset({ body: '', files: [] });
          this.notifications.success(REPLY_SENT_MESSAGE);
          this.focusBody();
        },
        error: (error: unknown) => this.showError(error, files),
      });
  }

  private showError(error: unknown, files: readonly File[]): void {
    // 422: the store replaces this area with "the ticket is closed".
    if (isTicketClosedError(error)) {
      return;
    }
    const filesError = attachmentServerError(error, files);
    this.filesError.set(filesError);
    const applied = applyServerErrors(this.form, error, { skipFields: [FILES_FIELD] });
    if (applied.unmatched.length === 0 && (filesError || applied.fields.length > 0)) {
      if (applied.fields.length > 0) {
        this.focusBody();
      } else {
        this.fileUpload().focus();
      }
      return;
    }

    const httpError = error instanceof HttpErrorResponse ? error : new HttpErrorResponse({ status: 500 });
    const alert: FormAlert =
      httpError.status === 409
        ? { title: 'הפנייה עודכנה בינתיים', detail: 'הנתונים נטענו מחדש. התגובה שלך לא נשלחה – אפשר לשלוח אותה שוב.' }
        : { title: toUserMessage(httpError) };
    if (applied.unmatched.length > 0) {
      alert.detail = applied.unmatched.join(' ');
    } else if (httpError.status !== 409 && isProblemDetails(httpError.error) && httpError.error.detail) {
      alert.detail = httpError.error.detail;
    }
    this.alert.set(alert);
  }

  private focusBody(): void {
    afterNextRender(() => this.bodyField().nativeElement.focus(), { injector: this.injector });
  }
}
