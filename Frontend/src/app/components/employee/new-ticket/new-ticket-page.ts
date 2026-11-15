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
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, map } from 'rxjs';

import { toUserMessage } from '../../../interceptors/error.interceptor';
import { isProblemDetails } from '../../../models/problem-details';
import { TICKET_FIELD_LIMITS } from '../../../models/ticket.models';
import { NotificationService } from '../../../services/notification.service';
import { FileUpload } from '../../shared/file-upload/file-upload';
import { FormAlert, FormAlertBox } from '../../shared/form-alert/form-alert';
import { PageHeader } from '../../shared/page-header/page-header';
import {
  FILES_FIELD,
  applyServerErrors,
  attachmentServerError,
  clearServerErrors,
} from '../../../services/server-errors';
import { HasUnsavedChanges, unsavedMessage } from '../../../guards/unsaved-changes.guard';
import { COUNTER_ANNOUNCE_DELAY_MS, describeCount, formatCount } from '../character-count';
import { MY_TICKETS_URL, ticketUrl } from '../employee-urls';
import { NewTicketStore } from '../../../services/new-ticket.store';

const { subjectMaxLength, descriptionMinLength, descriptionMaxLength } = TICKET_FIELD_LIMITS;

/** Required, and not only spaces. */
function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value?.trim() ? null : { required: true };
}

/** At least this many characters after trimming – the server trims too. */
function minTrimmedLength(min: number) {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const length = control.value?.trim().length ?? 0;
    return length > 0 && length < min ? { minlength: { requiredLength: min, actualLength: length } } : null;
  };
}

/** Order used to focus the first invalid field. */
const FIELD_ORDER = ['subject', 'description'] as const;

/**
 * "פנייה חדשה" (spec 7.4): optional subject, required free-text description, up to 3 files. Saved immediately;
 * classification and routing run in the background (the files are never sent to the AI).
 */
@Component({
  selector: 'app-new-ticket-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    PageHeader,
    FormAlertBox,
    FileUpload,
  ],
  providers: [NewTicketStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(window:beforeunload)': 'onBeforeUnload($event)' },
  templateUrl: './new-ticket-page.html',
  styleUrl: './new-ticket-page.scss',
})
export class NewTicketPage implements HasUnsavedChanges {
  protected readonly store = inject(NewTicketStore);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly fileUpload = viewChild.required(FileUpload);

  protected readonly myTicketsUrl = MY_TICKETS_URL;
  protected readonly limits = TICKET_FIELD_LIMITS;
  protected readonly descriptionMaxText = formatCount(descriptionMaxLength);

  protected readonly form = new FormGroup({
    subject: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(subjectMaxLength)] }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, minTrimmedLength(descriptionMinLength), Validators.maxLength(descriptionMaxLength)],
    }),
    files: new FormControl<File[]>([], { nonNullable: true }),
  });

  protected readonly alert = signal<FormAlert | null>(null);
  /** The server's error about the files (400 files / files[i], 413), next to the file picker. */
  protected readonly filesError = signal<string | null>(null);
  /** Sent successfully: leaving no longer asks for confirmation. */
  private submitted = false;

  private readonly subjectValue = toSignal(this.form.controls.subject.valueChanges, { initialValue: '' });
  private readonly descriptionValue = toSignal(this.form.controls.description.valueChanges, { initialValue: '' });
  protected readonly subjectLength = computed(() => this.subjectValue().length);
  protected readonly descriptionLength = computed(() => this.descriptionValue().length);

  /** Read to screen readers after the user pauses typing – not on every keystroke (spec 11.6). */
  protected readonly counterAnnouncement = toSignal(
    this.form.controls.description.valueChanges.pipe(
      debounceTime(COUNTER_ANNOUNCE_DELAY_MS),
      map((value) => describeCount(value, { min: descriptionMinLength, max: descriptionMaxLength })),
    ),
    { initialValue: '' },
  );

  constructor() {
    // The server's error is about the files that were sent: a new choice clears it.
    this.form.controls.files.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.filesError.set(null));
  }

  hasUnsavedChanges(): boolean {
    return !this.submitted && (this.hasText() || this.hasFiles());
  }

  unsavedChangesMessage(): string {
    return unsavedMessage(this.hasText(), this.hasFiles());
  }

  private hasText(): boolean {
    const { subject, description } = this.form.getRawValue();
    return subject.trim() !== '' || description.trim() !== '';
  }

  private hasFiles(): boolean {
    return this.form.controls.files.value.length > 0;
  }

  /** Refresh / closing the tab with text typed: the browser's own "leave site?" prompt. */
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  protected submit(): void {
    if (this.store.saving()) {
      return;
    }
    this.alert.set(null);
    this.filesError.set(null);
    clearServerErrors(this.form);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusFirstInvalid();
      return;
    }

    const { subject, description, files } = this.form.getRawValue();
    this.store
      .create({ subject: subject.trim() || null, description: description.trim(), files })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (created) => {
          this.submitted = true;
          this.notifications.success(`הפנייה התקבלה, מס' ${created.id}`);
          // replaceUrl: "back" from the ticket goes to the list, not to an empty form.
          void this.router.navigateByUrl(ticketUrl(created.id), { replaceUrl: true });
        },
        error: (error: unknown) => this.showError(error, files),
      });
  }

  /**
   * 400 next to the fields, errors about the files (400, 413) next to the file picker; anything else
   * (429 – too many tickets, network, 500) above the form.
   */
  private showError(error: unknown, files: readonly File[]): void {
    const filesError = attachmentServerError(error, files);
    this.filesError.set(filesError);
    const applied = applyServerErrors(this.form, error, { skipFields: [FILES_FIELD] });
    if (applied.unmatched.length === 0 && (filesError || applied.fields.length > 0)) {
      if (applied.fields.length > 0) {
        this.focusField(applied.fields[0]);
      } else {
        this.fileUpload().focus();
      }
      return;
    }
    const httpError = error instanceof HttpErrorResponse ? error : new HttpErrorResponse({ status: 500 });
    const alert: FormAlert = { title: toUserMessage(httpError) };
    if (applied.unmatched.length > 0) {
      alert.detail = applied.unmatched.join(' ');
    } else if (isProblemDetails(httpError.error) && httpError.error.detail) {
      alert.detail = httpError.error.detail;
    }
    this.alert.set(alert);
  }

  private focusFirstInvalid(): void {
    const name = FIELD_ORDER.find((field) => this.form.controls[field].invalid);
    if (name) {
      this.focusField(name);
    }
  }

  private focusField(name: string): void {
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLElement>(`[formControlName="${name}"]`)?.focus(),
      { injector: this.injector },
    );
  }
}
