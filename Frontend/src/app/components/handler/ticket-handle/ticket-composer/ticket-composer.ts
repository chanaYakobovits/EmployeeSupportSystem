import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { debounceTime, filter, map, of, switchMap } from 'rxjs';

import { toUserMessage } from '../../../../interceptors/error.interceptor';
import { isProblemDetails } from '../../../../models/problem-details';
import { COMMENT_FIELD_LIMITS } from '../../../../models/ticket.models';
import { ConfirmService } from '../../../../services/confirm.service';
import { NotificationService } from '../../../../services/notification.service';
import { ConfirmDialogData } from '../../../shared/confirm-dialog/confirm-dialog';
import {
  FILES_FIELD,
  applyServerErrors,
  attachmentServerError,
  clearServerErrors,
} from '../../../../services/server-errors';
import { AiSuggestion, TicketAiAnalysisStore } from '../../../../services/ticket-ai-analysis.store';
import { FilesNotAttachedError, TicketHandleStore, isConflictError } from '../../../../services/ticket-handle.store';
import { isTicketClosedError } from '../../../../services/ticket-view.store';
import { COUNTER_ANNOUNCE_DELAY_MS, describeCount, formatCount } from '../../../employee/character-count';
import { FileUpload } from '../../../shared/file-upload/file-upload';
import { FormAlert, FormAlertBox } from '../../../shared/form-alert/form-alert';
import { CONFLICT_MESSAGE } from '../../handler-messages';

const { bodyMaxLength } = COMMENT_FIELD_LIMITS;

/** Internal note (staff only) or a reply the employee sees. */
export type ComposerMode = 'internal' | 'reply';

export const INTERNAL_SAVED_MESSAGE = 'ההערה הפנימית נשמרה';
export const REPLY_SENT_MESSAGE = 'התגובה נשלחה לעובד';
export const EMPTY_INTERNAL_MESSAGE = 'יש לכתוב את ההערה הפנימית';
export const EMPTY_REPLY_MESSAGE = 'יש לכתוב תגובה או לצרף קובץ';
/** Same wording as the server's ChangeTicketStatusRequestValidator. */
export const EMPTY_INFO_REQUEST_MESSAGE = 'יש לכתוב לעובד איזה מידע חסר';
export const EMPTY_RESOLUTION_MESSAGE = 'יש לכתוב לעובד את הפתרון';
export const INFO_REQUESTED_MESSAGE = 'הבקשה למידע נשלחה לעובד';
export const RESOLUTION_SENT_MESSAGE = 'הפתרון נשלח לעובד';
export const FILES_NOT_ATTACHED_MESSAGE = 'הסטטוס עודכן וההודעה נשלחה, אך הקבצים לא צורפו';

export const RESOLVE_CONFIRM: ConfirmDialogData = {
  heading: 'שליחה כפתרון',
  message: 'הפנייה תסומן כנפתרה. העובד יוכל לאשר או לפתוח מחדש תוך 7 ימים.',
  confirmText: 'שליחה כפתרון',
};

/** Shown while the reply is based on the AI's suggestion (task 4.4). */
export const AI_DRAFT_NOTICE = 'טיוטה שנוצרה ע"י AI – יש לבדוק לפני שליחה';
export const AI_DRAFT_CLEARED = 'הטיוטה נמחקה';
/** The line before the missing information added to the reply. */
export const MISSING_INFO_INTRO = 'כדי שנוכל להמשיך בטיפול, נשמח לקבל את הפרטים הבאים:';

/** The missing information as a block of the reply: the intro line, then one item per line. */
export function missingInfoBlock(items: readonly string[]): string {
  return [MISSING_INFO_INTRO, ...items.map((item) => `- ${item}`)].join('\n');
}

/** What a send does: a comment / internal note, or a reply that also changes the status. */
export type SendKind = 'comment' | 'requestInfo' | 'resolve';

const MODE_ANNOUNCEMENTS: Record<ComposerMode, string> = {
  internal: 'מצב כתיבה: הערה פנימית – גלוי לצוות בלבד',
  reply: 'מצב כתיבה: תגובה לעובד – העובד יראה את ההודעה',
};

/**
 * The writing area of the handling screen (spec 7.7, area 3): two clearly different modes – "הערה פנימית" (yellowish,
 * "גלוי לצוות בלבד", the default) and "תגובה לעובד" (plain, "שליחה לעובד"). Only the modes the server allows are shown.
 * Switching keeps the text and the files: one form for both. The mode is announced to screen readers.
 */
@Component({
  selector: 'app-ticket-composer',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    FileUpload,
    FormAlertBox,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-composer.html',
  styleUrl: './ticket-composer.scss',
})
export class TicketComposer {
  /** A status change moved the ticket out of the user's scopes – the page goes back to the queue. */
  readonly left = output<void>();

  protected readonly store = inject(TicketHandleStore);
  /** The AI analysis of the page (task 4.4); absent outside the handling page. */
  private readonly ai = inject(TicketAiAnalysisStore, { optional: true });
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly bodyField = viewChild<ElementRef<HTMLTextAreaElement>>('bodyField');
  private readonly fileUpload = viewChild(FileUpload);

  protected readonly bodyMaxLength = bodyMaxLength;
  protected readonly bodyMaxText = formatCount(bodyMaxLength);

  protected readonly canInternal = computed(() => this.store.can('internalNote'));
  protected readonly canReply = computed(() => this.store.can('reply'));
  /** Both modes allowed: the switch is shown; otherwise the one mode, labelled. */
  protected readonly showSwitch = computed(() => this.canInternal() && this.canReply());
  /** The status sends of the reply mode – only when the server allows them. */
  protected readonly canRequestInfo = computed(() => this.canReply() && this.store.can('requestInfo'));
  protected readonly canResolve = computed(() => this.canReply() && this.store.can('resolve'));
  /** Which send the "required" error belongs to (its wording). */
  private readonly emptyKind = signal<SendKind>('comment');

  private readonly chosenMode = signal<ComposerMode>('internal');
  /** The chosen mode if allowed; otherwise the allowed one. The default is the internal note (no accidental reply). */
  readonly mode = computed<ComposerMode>(() => {
    const chosen = this.chosenMode();
    if (chosen === 'internal' && this.canInternal()) {
      return 'internal';
    }
    if (chosen === 'reply' && this.canReply()) {
      return 'reply';
    }
    return this.canInternal() ? 'internal' : 'reply';
  });
  protected readonly isInternal = computed(() => this.mode() === 'internal');
  protected readonly modeAnnouncement = signal('');

  /**
   * The text is based on the AI's suggestion (prefilled, or the missing information added) – until the field is empty
   * again. While true: the notice "טיוטה שנוצרה ע"י AI" and "נקה והתחל מאפס". Nothing is ever sent without a click.
   */
  protected readonly aiDraft = signal(false);
  protected readonly aiDraftNotice = AI_DRAFT_NOTICE;
  protected readonly draftAnnouncement = signal('');
  /** The AI text as it was put in the field; equal to the field = not edited by the user. */
  private draftText: string | null = null;
  /** The analysis whose suggestion was already offered: one prefill per analysis (not again after a send or "נקה"). */
  private usedSuggestionKey: string | null = null;

  protected readonly missingInformation = computed(() => this.ai?.missingInformation() ?? []);
  /** "הוספת המידע החסר" – in the reply mode, when the analysis lists something. */
  protected readonly canAddMissing = computed(() => !this.isInternal() && this.missingInformation().length > 0);

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
    this.form.controls.files.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.filesError.set(null));

    // Emptied (by the user, "נקה", or the reset after a send): no longer an AI draft.
    this.form.controls.body.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) => {
      if (this.aiDraft() && value.trim() === '') {
        this.aiDraft.set(false);
        this.draftText = null;
      }
    });

    // Spec 7.7 area 3: the reply mode with an empty field starts from the AI's suggested reply. Never over text.
    effect(() => {
      const mode = this.mode();
      const suggestion = this.ai?.suggestion() ?? null;
      untracked(() => {
        if (mode === 'reply' && suggestion) {
          this.prefill(suggestion);
        }
      });
    });

    // "בקשת מידע" / "שליחת פתרון" in the actions area: the reply mode, and the focus in the text.
    effect(() => {
      if (this.store.composerFocus() === null) {
        return;
      }
      untracked(() => {
        this.setMode('reply');
        this.focusBody();
      });
    });

    // A required error belongs to the mode it was raised in.
    effect(() => {
      this.mode();
      untracked(() => {
        if (this.form.controls.body.hasError('required')) {
          this.form.controls.body.setErrors(null);
          this.form.controls.body.updateValueAndValidity({ emitEvent: false });
        }
      });
    });
  }

  /** Text typed or files chosen – leaving the page asks first. An AI draft nobody edited is not the user's text. */
  hasText(): boolean {
    const value = this.bodyValue();
    if (this.aiDraft() && value === this.draftText) {
      return false;
    }
    return value.trim() !== '';
  }

  hasFiles(): boolean {
    return this.filesValue().length > 0;
  }

  /** Switching keeps the text and the files (one form for both modes). */
  protected setMode(mode: ComposerMode): void {
    if (mode === this.mode()) {
      return;
    }
    // A reply suggestion nobody edited must not become an internal note by accident.
    if (mode === 'internal' && this.aiDraft() && this.form.controls.body.value === this.draftText) {
      this.form.controls.body.setValue('');
    }
    this.chosenMode.set(mode);
    this.alert.set(null);
    this.modeAnnouncement.set(MODE_ANNOUNCEMENTS[this.mode()]);
  }

  /** "נקה והתחל מאפס": the AI draft goes, the user writes from scratch. */
  protected clearDraft(): void {
    this.form.controls.body.setValue('');
    this.draftAnnouncement.set(AI_DRAFT_CLEARED);
    this.focusBody();
  }

  /** Adds the analysis' missing information at the end of the text (useful for "שליחה ובקשת מידע"). */
  protected addMissingInfo(): void {
    const items = this.missingInformation();
    if (items.length === 0) {
      return;
    }
    const body = this.form.controls.body;
    const current = body.value;
    const untouched = current.trim() === '' || (this.aiDraft() && current === this.draftText);
    const block = missingInfoBlock(items);
    const next = current.trim() === '' ? block : `${current.trimEnd()}\n\n${block}`;
    body.setValue(next);
    body.markAsDirty();
    this.draftText = untouched ? next : this.draftText;
    this.aiDraft.set(true);
    this.focusBody();
  }

  /** The first time the reply mode meets this analysis: fill the field if it is empty. */
  private prefill(suggestion: AiSuggestion): void {
    if (suggestion.key === this.usedSuggestionKey) {
      return;
    }
    this.usedSuggestionKey = suggestion.key;
    const body = this.form.controls.body;
    if (body.value.trim() !== '' || this.store.busy()) {
      return;
    }
    body.setValue(suggestion.text);
    this.draftText = suggestion.text;
    this.aiDraft.set(true);
  }

  /** The form's submit (Enter, "שליחה לעובד" / "שמירת הערה פנימית"): a comment without a status change. */
  protected submit(): void {
    this.send('comment');
  }

  /**
   * One of the sends: a comment (or internal note), or a reply with a status change – "שליחה ובקשת מידע"
   * (WaitingForEmployee) or "שליחה כפתרון" (Resolved, after a confirmation). A status change needs text: it is the
   * message the employee gets (files alone are not enough).
   */
  protected send(kind: SendKind): void {
    if (this.store.busy()) {
      return;
    }
    this.alert.set(null);
    this.filesError.set(null);
    clearServerErrors(this.form);
    this.emptyKind.set(kind);

    const internal = kind === 'comment' && this.isInternal();
    const body = this.form.controls.body.value.trim();
    const files = this.form.controls.files.value;
    // An internal note needs text (its files are internal through it); a reply may be files only.
    if (body === '' && (kind !== 'comment' || internal || files.length === 0)) {
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

    if (kind === 'comment') {
      this.store
        .sendComment(body, files, internal)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => this.sent(internal ? INTERNAL_SAVED_MESSAGE : REPLY_SENT_MESSAGE),
          error: (error: unknown) => this.showError(error, files),
        });
      return;
    }

    const confirmed = kind === 'resolve' ? this.confirm.confirm(RESOLVE_CONFIRM) : of(true);
    confirmed
      .pipe(
        filter(Boolean),
        switchMap(() =>
          this.store.changeStatus(kind === 'resolve' ? 'Resolved' : 'WaitingForEmployee', body, files),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (outcome) => {
          if (outcome === 'left') {
            this.form.reset({ body: '', files: [] });
            this.left.emit();
            return;
          }
          this.sent(kind === 'resolve' ? RESOLUTION_SENT_MESSAGE : INFO_REQUESTED_MESSAGE);
        },
        error: (error: unknown) => {
          if (error instanceof FilesNotAttachedError) {
            this.filesNotAttached(error, files);
          } else {
            this.showError(error, files);
          }
        },
      });
  }

  protected emptyMessage(): string {
    switch (this.emptyKind()) {
      case 'requestInfo':
        return EMPTY_INFO_REQUEST_MESSAGE;
      case 'resolve':
        return EMPTY_RESOLUTION_MESSAGE;
      default:
        return this.isInternal() ? EMPTY_INTERNAL_MESSAGE : EMPTY_REPLY_MESSAGE;
    }
  }

  /** Sent: the store already reloaded the status, the timeline and the allowed actions. */
  private sent(message: string): void {
    this.form.reset({ body: '', files: [] });
    this.notifications.success(message);
    this.focusBody();
  }

  /** The status changed and the message was sent; only the files failed. The text goes, the files stay. */
  private filesNotAttached(error: FilesNotAttachedError, files: readonly File[]): void {
    this.form.controls.body.reset('');
    this.filesError.set(attachmentServerError(error.filesError, files));
    const httpError =
      error.filesError instanceof HttpErrorResponse ? error.filesError : new HttpErrorResponse({ status: 500 });
    this.alert.set({ title: FILES_NOT_ATTACHED_MESSAGE, detail: toUserMessage(httpError) });
  }

  /** The text and files stay in the form whatever the error. */
  private showError(error: unknown, files: readonly File[]): void {
    if (isConflictError(error)) {
      this.alert.set({ title: CONFLICT_MESSAGE, detail: 'מה שכתבת לא נשלח ונשאר כאן.' });
      return;
    }
    if (isTicketClosedError(error)) {
      this.alert.set({ title: 'הפנייה נסגרה ולא ניתן להוסיף לה תגובות או הערות' });
      return;
    }
    const filesError = attachmentServerError(error, files);
    this.filesError.set(filesError);
    // PATCH /status names the text "message"; the comment endpoints name it "body".
    const applied = applyServerErrors(this.form, error, { skipFields: [FILES_FIELD], fieldAliases: { message: 'body' } });
    if (applied.unmatched.length === 0 && (filesError || applied.fields.length > 0)) {
      if (applied.fields.length > 0) {
        this.focusBody();
      } else {
        this.fileUpload()?.focus();
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

  private focusBody(): void {
    afterNextRender(() => this.bodyField()?.nativeElement.focus(), { injector: this.injector });
  }
}
