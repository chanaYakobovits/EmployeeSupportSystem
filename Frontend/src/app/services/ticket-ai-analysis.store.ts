import { DestroyRef, Injectable, NgZone, computed, effect, inject, signal, untracked } from '@angular/core';
import { Observable, Subscription, catchError, defer, finalize, map, switchMap, tap, throwError } from 'rxjs';

import { AiAnalysisDetails, TicketAiAnalysis, isAnalysisInProgress } from '../models/ai-analysis.models';
import { ChangeOutcome, TicketHandleStore, isConflictError } from './ticket-handle.store';
import { TicketsApiService } from './tickets-api.service';

/** How often a running analysis is checked again. */
export const AI_POLL_INTERVAL_MS = 3_000;
/** How long it is checked automatically; after that – "רענון" by hand. */
export const AI_POLL_MAX_MS = 60_000;

/** The reason saved with "החל המלצה" (PATCH /classification, shown in the staff timeline). */
export const AI_RECOMMENDATION_REASON = 'לפי המלצת AI';

export const AI_IN_PROGRESS_ANNOUNCEMENT = 'ניתוח ה-AI בתהליך';
export const AI_DONE_ANNOUNCEMENT = 'ניתוח ה-AI הסתיים';
export const AI_FAILED_ANNOUNCEMENT = 'ניתוח ה-AI נכשל';

export type AiAnalysisLoadStatus = 'idle' | 'loading' | 'ready' | 'error';

/** The suggested reply of one analysis, for the writing area (one prefill per analysis). */
export interface AiSuggestion {
  /** Identifies the analysis (a re-analysis gives a new one). */
  key: string;
  text: string;
}

/**
 * The AI analysis of the handling screen (task 4.4, spec 7.7 area 2). Loaded when the server allows viewAiAnalysis, and
 * again whenever the ticket's version changes (an applied recommendation changes what "matches" means). While an
 * analysis is waiting or running it is checked every few seconds, up to a minute; when it ends the ticket is reloaded
 * quietly (its version and maybe its classification changed). The timer runs outside the Angular zone, like
 * ClockService, so it never keeps the app "unstable". Provided by TicketHandlePage next to TicketHandleStore.
 */
@Injectable()
export class TicketAiAnalysisStore {
  private readonly api = inject(TicketsApiService);
  private readonly handle = inject(TicketHandleStore);
  private readonly zone = inject(NgZone);

  private readonly dataSignal = signal<TicketAiAnalysis | null>(null);
  private readonly statusSignal = signal<AiAnalysisLoadStatus>('idle');
  private readonly pollingSignal = signal(false);
  private readonly stalledSignal = signal(false);
  private readonly busySignal = signal(false);
  private readonly announcementSignal = signal('');

  private ticketId: number | null = null;
  private request?: Subscription;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private pollStartedAt: number | null = null;

  readonly data = this.dataSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  /** Being checked again automatically. */
  readonly polling = this.pollingSignal.asReadonly();
  /** Still running after a minute of checks – the screen offers "רענון". */
  readonly stalled = this.stalledSignal.asReadonly();
  /** "נתח מחדש" or "החל המלצה" is being sent. */
  readonly busy = this.busySignal.asReadonly();
  /** For an aria-live region: the analysis started / ended. */
  readonly announcement = this.announcementSignal.asReadonly();

  readonly analysis = computed<AiAnalysisDetails | null>(() => this.dataSignal()?.analysis ?? null);
  readonly inProgress = computed(() => {
    const data = this.dataSignal();
    return data !== null && isAnalysisInProgress(data.processing.status);
  });
  /** The suggested reply for the writing area – none while a new analysis is on its way. */
  readonly suggestion = computed<AiSuggestion | null>(() => {
    const analysis = this.analysis();
    const text = analysis?.suggestedSolution.trim() ?? '';
    return analysis && text !== '' && !this.inProgress() ? { key: analysis.createdAt, text } : null;
  });
  readonly missingInformation = computed(() => this.analysis()?.missingInformation ?? []);

  constructor() {
    // The ticket's version: every reload of the ticket (a change, a refresh) – the analysis is read again.
    effect(() => {
      const ticket = this.handle.ticket();
      const allowed = this.handle.can('viewAiAnalysis');
      untracked(() => {
        if (ticket && allowed) {
          this.load(ticket.id, ticket.id === this.ticketId);
        } else {
          this.reset();
        }
      });
    });
    inject(DestroyRef).onDestroy(() => this.reset());
  }

  /** "רענון" after a minute of checks: checks again, for another minute. */
  refresh(): void {
    if (this.ticketId !== null) {
      this.pollStartedAt = null;
      this.load(this.ticketId, true);
    }
  }

  /** "נתח מחדש": a new job; the ticket's new version is loaded, then the analysis is checked until it ends. */
  retry(): Observable<void> {
    const id = this.ticketId;
    if (id === null) {
      return throwError(() => new Error('No ticket loaded.'));
    }
    return defer(() => {
      this.busySignal.set(true);
      return this.api.retryAiAnalysis(id).pipe(
        catchError((error: unknown) => {
          if (isConflictError(error)) {
            this.handle.refresh().subscribe();
          }
          return throwError(() => error);
        }),
        tap(() => (this.pollStartedAt = null)),
        // The reload of the ticket changes its version → the effect reads the analysis (Pending) → checks start.
        switchMap(() => this.handle.refresh()),
        map(() => undefined),
        finalize(() => this.busySignal.set(false)),
      );
    });
  }

  /**
   * "החל המלצה": the recommended category and urgency through the reclassification (→ a manual classification, with
   * the fixed reason). The caller confirms first and handles the outcome like the reclassification dialog's.
   */
  apply(): Observable<ChangeOutcome> {
    const analysis = this.analysis();
    if (!analysis || analysis.categoryId === null) {
      return throwError(() => new Error('Nothing to apply.'));
    }
    return defer(() => {
      this.busySignal.set(true);
      return this.handle
        .reclassify({ categoryId: analysis.categoryId!, urgency: analysis.urgency, reason: AI_RECOMMENDATION_REASON })
        .pipe(finalize(() => this.busySignal.set(false)));
    });
  }

  private load(id: number, quiet: boolean): void {
    this.clearTimer();
    this.request?.unsubscribe();
    if (id !== this.ticketId) {
      this.ticketId = id;
      this.dataSignal.set(null);
      this.pollStartedAt = null;
      this.pollingSignal.set(false);
      this.stalledSignal.set(false);
    }
    if (!quiet || this.dataSignal() === null) {
      this.statusSignal.set('loading');
    }

    this.request = this.api.aiAnalysis(id).subscribe({
      next: (data) => this.show(id, data),
      error: () => {
        this.stopPolling();
        // A quiet reload that failed keeps what is shown.
        if (this.dataSignal() === null) {
          this.statusSignal.set('error');
        }
      },
    });
  }

  private show(id: number, data: TicketAiAnalysis): void {
    if (id !== this.ticketId) {
      return;
    }
    const wasPolling = this.pollingSignal();
    this.dataSignal.set(data);
    this.statusSignal.set('ready');

    if (isAnalysisInProgress(data.processing.status)) {
      this.continuePolling(id);
      return;
    }

    this.stopPolling();
    if (wasPolling) {
      this.announcementSignal.set(data.processing.status === 'Failed' ? AI_FAILED_ANNOUNCEMENT : AI_DONE_ANNOUNCEMENT);
      // The worker changed the ticket (AiStatus, maybe the classification): the next change needs its new version.
      this.handle.refresh().subscribe();
    }
  }

  private continuePolling(id: number): void {
    const now = Date.now();
    if (this.pollStartedAt === null) {
      this.pollStartedAt = now;
      this.stalledSignal.set(false);
      this.announcementSignal.set(AI_IN_PROGRESS_ANNOUNCEMENT);
    }
    if (now - this.pollStartedAt >= AI_POLL_MAX_MS) {
      this.pollingSignal.set(false);
      this.stalledSignal.set(true);
      return;
    }

    this.pollingSignal.set(true);
    this.timer = this.zone.runOutsideAngular(() =>
      setTimeout(() => {
        this.timer = null;
        this.zone.run(() => this.load(id, true));
      }, AI_POLL_INTERVAL_MS),
    );
  }

  private stopPolling(): void {
    this.clearTimer();
    this.pollStartedAt = null;
    this.pollingSignal.set(false);
    this.stalledSignal.set(false);
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private reset(): void {
    this.clearTimer();
    this.request?.unsubscribe();
    this.ticketId = null;
    this.dataSignal.set(null);
    this.statusSignal.set('idle');
    this.pollStartedAt = null;
    this.pollingSignal.set(false);
    this.stalledSignal.set(false);
  }
}
