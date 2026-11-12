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

import { toUserMessage } from '../../../core/interceptors/error.interceptor';
import { isProblemDetails } from '../../../core/models/problem-details';
import { TICKET_FIELD_LIMITS } from '../../../core/models/ticket.models';
import { NotificationService } from '../../../core/notifications/notification.service';
import { FormAlert, FormAlertBox } from '../../../shared/components/form-alert/form-alert';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { applyServerErrors, clearServerErrors } from '../../../shared/forms/server-errors';
import { HasUnsavedChanges } from '../../../shared/forms/unsaved-changes.guard';
import { MY_TICKETS_URL, ticketUrl } from '../employee-urls';
import { NewTicketStore } from './new-ticket.store';

const { subjectMaxLength, descriptionMinLength, descriptionMaxLength } = TICKET_FIELD_LIMITS;

/** Wait this long after the last keystroke before announcing the character count to screen readers. */
export const COUNTER_ANNOUNCE_DELAY_MS = 800;

const NUMBER = new Intl.NumberFormat('he-IL');

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
 * "פנייה חדשה" (spec 7.4): optional subject, required free-text description. Saved immediately; classification and
 * routing run in the background. Attachments come in task 3.2.
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

  protected readonly myTicketsUrl = MY_TICKETS_URL;
  protected readonly limits = TICKET_FIELD_LIMITS;
  protected readonly descriptionMaxText = NUMBER.format(descriptionMaxLength);

  protected readonly form = new FormGroup({
    subject: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(subjectMaxLength)] }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, minTrimmedLength(descriptionMinLength), Validators.maxLength(descriptionMaxLength)],
    }),
  });

  protected readonly alert = signal<FormAlert | null>(null);
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
      map((value) => describeCount(value.trim().length, value.length)),
    ),
    { initialValue: '' },
  );

  hasUnsavedChanges(): boolean {
    if (this.submitted) {
      return false;
    }
    const { subject, description } = this.form.getRawValue();
    return subject.trim() !== '' || description.trim() !== '';
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
    clearServerErrors(this.form);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusFirstInvalid();
      return;
    }

    const { subject, description } = this.form.getRawValue();
    this.store
      .create({ subject: subject.trim() || null, description: description.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (created) => {
          this.submitted = true;
          this.notifications.success(`הפנייה התקבלה, מס' ${created.id}`);
          // replaceUrl: "back" from the ticket goes to the list, not to an empty form.
          void this.router.navigateByUrl(ticketUrl(created.id), { replaceUrl: true });
        },
        error: (error: unknown) => this.showError(error),
      });
  }

  /** 400 next to the fields; anything else (429 – too many tickets, network, 500) above the form. */
  private showError(error: unknown): void {
    const applied = applyServerErrors(this.form, error);
    if (applied.fields.length > 0 && applied.unmatched.length === 0) {
      this.focusField(applied.fields[0]);
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

/** "נותרו 3,950 תווים" / "נדרשים עוד 4 תווים לפחות". */
function describeCount(trimmedLength: number, length: number): string {
  if (length === 0) {
    return '';
  }
  if (trimmedLength < descriptionMinLength) {
    const missing = descriptionMinLength - trimmedLength;
    return missing === 1 ? 'נדרש עוד תו אחד לפחות' : `נדרשים עוד ${missing} תווים לפחות`;
  }
  const remaining = descriptionMaxLength - length;
  if (remaining <= 0) {
    return 'הגעת למספר התווים המרבי';
  }
  return remaining === 1 ? 'נותר תו אחד' : `נותרו ${NUMBER.format(remaining)} תווים`;
}
