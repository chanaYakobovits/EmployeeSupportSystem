import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { filter, switchMap } from 'rxjs';

import { toUserMessage } from '../../../../interceptors/error.interceptor';
import { AiAnalysisDetails, AiConfidenceLevel, AiDisposition } from '../../../../models/ai-analysis.models';
import { isProblemDetails } from '../../../../models/problem-details';
import { DateTimePipe } from '../../../../pipes/date-time.pipe';
import { urgencyLabel } from '../../../../pipes/urgency-label.pipe';
import { ConfirmService } from '../../../../services/confirm.service';
import { AI_RECOMMENDATION_REASON, TicketAiAnalysisStore } from '../../../../services/ticket-ai-analysis.store';
import { ChangeOutcome, TicketHandleStore, isConflictError } from '../../../../services/ticket-handle.store';
import { ConfirmDialogData } from '../../../shared/confirm-dialog/confirm-dialog';
import { FormAlert, FormAlertBox } from '../../../shared/form-alert/form-alert';
import { UrgencyBadge } from '../../../shared/urgency-badge/urgency-badge';
import { CONFLICT_MESSAGE } from '../../handler-messages';

export const APPLIED_LABEL = 'הוחל על הפנייה';
export const RECOMMENDATION_LABEL = 'המלצה בלבד – לא הוחלה';
export const IN_PROGRESS_TEXT = 'הניתוח בתהליך...';
export const STALLED_TEXT = 'הניתוח נמשך יותר מהרגיל.';
export const RETRY_QUEUED_MESSAGE = 'הניתוח נשלח מחדש';

/** Why the analysis was not applied (Disposition). */
export const NOT_APPLIED_REASONS: Record<Exclude<AiDisposition, 'Applied'>, string> = {
  LowConfidence: 'רמת הביטחון נמוכה, ולכן הפנייה נותבה לתור הכללי',
  UnknownCategory: 'הקטגוריה שהוצעה אינה קיימת או אינה פעילה',
  ManuallyClassified: 'הפנייה כבר סווגה ידנית',
  AlreadyRouted: 'הפנייה כבר נותבה לפני הניתוח',
};

export const CONFIDENCE_LABELS: Record<AiConfidenceLevel, string> = {
  High: 'גבוה',
  Medium: 'בינוני',
  Low: 'נמוך',
};

/** "גבוה · 86%" */
export function confidenceText(analysis: Pick<AiAnalysisDetails, 'confidence' | 'confidenceLevel'>): string {
  return `${CONFIDENCE_LABELS[analysis.confidenceLevel]} · ${Math.round(analysis.confidence * 100)}%`;
}

export function applyConfirm(analysis: AiAnalysisDetails): ConfirmDialogData {
  return {
    heading: 'החלת המלצת ה-AI',
    message: `הפנייה תסווג כ"${analysis.categoryName ?? analysis.categoryCode}" בדחיפות ${urgencyLabel(analysis.urgency)}.`,
    consequences: [`הנימוק שיירשם בהיסטוריה: "${AI_RECOMMENDATION_REASON}"`, 'הסיווג ייחשב סיווג ידני'],
    confirmText: 'החלת ההמלצה',
  };
}

/**
 * Area 2 of the handling screen (spec 7.7, task 4.4): what the AI suggested and how much to trust it – the category and
 * urgency, the confidence as a word and a percentage, the summary, the missing information, and whether it was applied
 * (or why not). "החל המלצה" and "נתח מחדש" only when the server allows them. A running analysis is announced and
 * checked again by the store. The decision is always the handler's: nothing here reaches the employee.
 */
@Component({
  selector: 'app-ai-analysis-panel',
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, DateTimePipe, FormAlertBox, UrgencyBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ai-analysis-panel.html',
  styleUrl: './ai-analysis-panel.scss',
})
export class AiAnalysisPanel {
  /** "החל המלצה" changed the classification: the page shows the outcome (or goes back when out of scope). */
  readonly applied = output<ChangeOutcome>();

  protected readonly ai = inject(TicketAiAnalysisStore);
  protected readonly handle = inject(TicketHandleStore);
  private readonly confirm = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly alert = signal<FormAlert | null>(null);
  protected readonly appliedLabel = APPLIED_LABEL;
  protected readonly recommendationLabel = RECOMMENDATION_LABEL;
  protected readonly inProgressText = IN_PROGRESS_TEXT;
  protected readonly stalledText = STALLED_TEXT;

  protected readonly processing = computed(() => this.ai.data()?.processing ?? null);
  protected readonly failed = computed(() => {
    const status = this.processing()?.status;
    return status === 'Failed' || status === 'Skipped';
  });
  protected readonly notAppliedReason = computed(() => {
    const analysis = this.ai.analysis();
    return analysis && analysis.disposition !== 'Applied' ? NOT_APPLIED_REASONS[analysis.disposition] : null;
  });
  protected readonly confidence = computed(() => {
    const analysis = this.ai.analysis();
    return analysis ? confidenceText(analysis) : '';
  });
  protected readonly canRetry = computed(() => this.handle.can('retryAnalysis'));
  protected readonly canApply = computed(
    () => this.handle.can('applyRecommendation') && this.ai.analysis()?.categoryId != null,
  );
  /** The suggested reply goes to the writing area, in the reply mode. */
  protected readonly canUseSuggestion = computed(() => this.handle.can('reply') && this.ai.suggestion() !== null);
  protected readonly disabled = computed(() => this.ai.busy() || this.handle.busy());

  protected retry(): void {
    this.alert.set(null);
    this.ai
      .retry()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ error: (error: unknown) => this.showError(error) });
  }

  protected apply(): void {
    const analysis = this.ai.analysis();
    if (!analysis) {
      return;
    }
    this.alert.set(null);
    this.confirm
      .confirm(applyConfirm(analysis))
      .pipe(
        filter(Boolean),
        switchMap(() => this.ai.apply()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (outcome) => this.applied.emit(outcome),
        error: (error: unknown) => this.showError(error),
      });
  }

  protected useSuggestion(): void {
    this.handle.focusReply();
  }

  /** 409 → the ticket was reloaded; 422 / 429 → the server's own words (AI unavailable, too soon...). */
  private showError(error: unknown): void {
    if (isConflictError(error)) {
      this.alert.set({ title: CONFLICT_MESSAGE });
      return;
    }
    const httpError = error instanceof HttpErrorResponse ? error : new HttpErrorResponse({ status: 500 });
    const problem = isProblemDetails(httpError.error) ? httpError.error : null;
    this.alert.set({
      title: problem?.title ?? toUserMessage(httpError),
      ...(problem?.detail ? { detail: problem.detail } : {}),
    });
  }
}
