import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { EMPTY, of, throwError } from 'rxjs';

import { AiAnalysisDetails, TicketAiAnalysis } from '../../../models/ai-analysis.models';
import { TicketAction } from '../../../models/ticket.models';
import { AttachmentDownloadService } from '../../../services/attachment-download.service';
import { AuthService } from '../../../services/auth.service';
import { CategoriesApiService } from '../../../services/categories-api.service';
import { ConfirmService } from '../../../services/confirm.service';
import { NotificationService } from '../../../services/notification.service';
import { AI_RECOMMENDATION_REASON } from '../../../services/ticket-ai-analysis.store';
import { TicketsApiService } from '../../../services/tickets-api.service';
import { fakeAuth, fakeStaffDetails, fakeStaffHistory } from '../testing/handler-fixtures';
import { APPLIED_LABEL, NOT_APPLIED_REASONS, RECOMMENDATION_LABEL } from './ai-analysis-panel/ai-analysis-panel';
import { AI_DRAFT_NOTICE, MISSING_INFO_INTRO } from './ticket-composer/ticket-composer';
import { TicketHandlePage } from './ticket-handle-page';

@Component({ template: '' })
class Stub {}

const STAFF: TicketAction[] = ['reply', 'internalNote', 'reclassify', 'viewAiAnalysis', 'requestInfo', 'resolve'];

function analysis(overrides: Partial<AiAnalysisDetails> = {}): AiAnalysisDetails {
  return {
    categoryCode: 'IT',
    categoryName: 'מחשוב',
    categoryId: 3,
    urgency: 'High',
    confidence: 0.86,
    confidenceLevel: 'High',
    suggestedSubject: 'מדפסת',
    summary: 'המדפסת בקומה 2 לא מדפיסה',
    missingInformation: ['דגם המדפסת', 'הודעת השגיאה'],
    suggestedSolution: 'שלום, נא לנסות להתקין מחדש את מנהל ההתקן.',
    model: 'gpt-4o-mini',
    promptVersion: 'v1',
    createdAt: '2026-11-16T08:01:00Z',
    verdict: 'Valid',
    disposition: 'Applied',
    applied: true,
    matchesCurrentClassification: true,
    redactedInput: 'המדפסת של [NAME] לא מדפיסה',
    ...overrides,
  };
}

function aiData(overrides: Partial<AiAnalysisDetails> = {}, status: TicketAiAnalysis['processing']['status'] = 'Completed'): TicketAiAnalysis {
  return {
    processing: { status, errorCode: null, errorMessage: null, requestedAt: '2026-11-16T08:00:00Z', nextAttemptAt: null },
    analysis: analysis(overrides),
  };
}

describe('TicketHandlePage – AI analysis (task 4.4)', () => {
  let api: jasmine.SpyObj<TicketsApiService>;
  let confirm: jasmine.SpyObj<ConfirmService>;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    api = jasmine.createSpyObj<TicketsApiService>('TicketsApiService', [
      'get',
      'history',
      'addComment',
      'addAttachments',
      'reclassify',
      'assign',
      'assignOptions',
      'changeStatus',
      'aiAnalysis',
      'retryAiAnalysis',
    ]);
    api.get.and.returnValue(of(fakeStaffDetails({ allowedActions: STAFF })));
    api.history.and.returnValue(of(fakeStaffHistory()));
    api.aiAnalysis.and.returnValue(of(aiData()));
    confirm = jasmine.createSpyObj<ConfirmService>('ConfirmService', ['confirm']);
    confirm.confirm.and.returnValue(of(true));
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'handler/assigned', component: Stub },
            { path: 'handler/tickets/:id', component: TicketHandlePage },
          ],
          withComponentInputBinding(),
        ),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: TicketsApiService, useValue: api },
        { provide: NotificationService, useValue: jasmine.createSpyObj('NotificationService', ['success', 'error']) },
        { provide: ConfirmService, useValue: confirm },
        { provide: AuthService, useValue: fakeAuth() },
        { provide: AttachmentDownloadService, useValue: { download: () => EMPTY } },
        { provide: CategoriesApiService, useValue: { list: () => of([]) } },
      ],
    });
  });

  async function open(actions: TicketAction[] = STAFF): Promise<HTMLElement> {
    api.get.and.returnValue(of(fakeStaffDetails({ allowedActions: actions })));
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/handler/tickets/152', TicketHandlePage);
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  function panel(element: HTMLElement): HTMLElement {
    return element.querySelector<HTMLElement>('app-ai-analysis-panel')!;
  }

  function text(element: HTMLElement): HTMLTextAreaElement {
    return element.querySelector<HTMLTextAreaElement>('app-ticket-composer textarea')!;
  }

  function toReply(element: HTMLElement): void {
    element.querySelector<HTMLButtonElement>('mat-button-toggle[data-mode="reply"] button')!.click();
    harness.detectChanges();
  }

  function toInternal(element: HTMLElement): void {
    element.querySelector<HTMLButtonElement>('mat-button-toggle[data-mode="internal"] button')!.click();
    harness.detectChanges();
  }

  function type(field: HTMLTextAreaElement, value: string): void {
    field.value = value;
    field.dispatchEvent(new Event('input'));
    harness.detectChanges();
  }

  // ---- area 2 ----

  it('shows the suggestion, the confidence as a word and a percentage, the missing information and "applied"', async () => {
    const element = await open();
    const area = panel(element);

    expect(area.textContent).toContain('מחשוב');
    expect(area.textContent).toContain('גבוה · 86%');
    expect(area.textContent).toContain('המדפסת בקומה 2 לא מדפיסה');
    expect(Array.from(area.querySelectorAll('.missing li')).map((li) => li.textContent!.trim())).toEqual([
      'דגם המדפסת',
      'הודעת השגיאה',
    ]);
    expect(area.querySelector('[data-disposition="applied"]')!.textContent).toContain(APPLIED_LABEL);
    expect(area.querySelector('[data-disposition="recommendation"]')).toBeNull();
    expect(area.querySelector('details.sent')!.textContent).toContain('המדפסת של [NAME] לא מדפיסה');
  });

  it('marks a recommendation that was not applied, with the reason', async () => {
    api.aiAnalysis.and.returnValue(
      of(aiData({ disposition: 'LowConfidence', applied: false, confidence: 0.41, confidenceLevel: 'Low' })),
    );
    const element = await open();
    const mark = panel(element).querySelector('[data-disposition="recommendation"]')!;

    expect(mark.textContent).toContain(RECOMMENDATION_LABEL);
    expect(mark.textContent).toContain(NOT_APPLIED_REASONS.LowConfidence);
    expect(panel(element).textContent).toContain('נמוך · 41%');
  });

  it('shows the manual-classification reason', async () => {
    api.aiAnalysis.and.returnValue(of(aiData({ disposition: 'ManuallyClassified', applied: false })));
    const element = await open();

    expect(panel(element).textContent).toContain(NOT_APPLIED_REASONS.ManuallyClassified);
  });

  it('applies the recommendation through the reclassification, with the fixed reason, after a confirmation', async () => {
    api.aiAnalysis.and.returnValue(of(aiData({ disposition: 'LowConfidence', applied: false, urgency: 'Critical' })));
    api.reclassify.and.returnValue(of({ id: 152, rowVersion: 'AAAAAAAAB9I=', canView: true }));
    const element = await open([...STAFF, 'applyRecommendation']);

    panel(element).querySelector<HTMLButtonElement>('[data-action="applyRecommendation"]')!.click();
    harness.detectChanges();

    expect(confirm.confirm).toHaveBeenCalled();
    expect(api.reclassify).toHaveBeenCalledWith(152, {
      categoryId: 3,
      urgency: 'Critical',
      reason: AI_RECOMMENDATION_REASON,
      rowVersion: 'AAAAAAAAB9E=',
    });
  });

  it('offers "החל המלצה" only when the server allows it', async () => {
    const element = await open();

    expect(panel(element).querySelector('[data-action="applyRecommendation"]')).toBeNull();
  });

  it('a failed analysis shows the message and "נתח מחדש"', async () => {
    api.aiAnalysis.and.returnValue(
      of({
        processing: {
          status: 'Failed',
          errorCode: 'Timeout',
          errorMessage: 'שירות ה-AI לא הגיב בזמן',
          requestedAt: null,
          nextAttemptAt: null,
        },
        analysis: null,
      } satisfies TicketAiAnalysis),
    );
    api.retryAiAnalysis.and.returnValue(of({ id: 152, rowVersion: 'AAAAAAAAB9I=', canView: true }));
    const element = await open([...STAFF, 'retryAnalysis']);
    const failed = panel(element).querySelector('[data-state="failed"]')!;

    expect(failed.textContent).toContain('שירות ה-AI לא הגיב בזמן');
    failed.querySelector<HTMLButtonElement>('[data-action="retryAnalysis"]')!.click();
    harness.detectChanges();

    expect(api.retryAiAnalysis).toHaveBeenCalledWith(152);
  });

  it('shows the server\'s words when the AI is unavailable', async () => {
    api.aiAnalysis.and.returnValue(of(aiData()));
    api.retryAiAnalysis.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 422,
            error: { title: 'שירות ה-AI אינו זמין כרגע', status: 422, type: 'x/ai-unavailable' },
          }),
      ),
    );
    const element = await open([...STAFF, 'retryAnalysis']);

    panel(element).querySelector<HTMLButtonElement>('[data-action="retryAnalysis"]')!.click();
    harness.detectChanges();

    expect(panel(element).querySelector('app-form-alert')!.textContent).toContain('שירות ה-AI אינו זמין כרגע');
  });

  it('a running analysis says so, for screen readers too', async () => {
    api.aiAnalysis.and.returnValue(of({ ...aiData(), analysis: null, processing: { ...aiData().processing, status: 'Processing' } }));
    const element = await open();

    expect(panel(element).querySelector('[data-state="in-progress"]')!.textContent).toContain('הניתוח בתהליך...');
    expect(panel(element).querySelector('[aria-live="polite"]')!.textContent).toContain('ניתוח ה-AI בתהליך');
  });

  it('a requester view never loads the analysis', async () => {
    const element = await open(['reply', 'internalNote']);

    expect(panel(element)).toBeNull();
    expect(api.aiAnalysis).not.toHaveBeenCalled();
  });

  // ---- area 3 ----

  it('the reply mode starts from the suggested reply, marked as an AI draft', async () => {
    const element = await open();
    expect(text(element).value).toBe('');

    toReply(element);

    expect(text(element).value).toBe('שלום, נא לנסות להתקין מחדש את מנהל ההתקן.');
    const draft = element.querySelector('app-ticket-composer [data-state="ai-draft"]')!;
    expect(draft.getAttribute('role')).toBe('status');
    expect(draft.textContent).toContain(AI_DRAFT_NOTICE);
  });

  it('never replaces what the user wrote', async () => {
    const element = await open();
    type(text(element), 'כתבתי בעצמי');

    toReply(element);

    expect(text(element).value).toBe('כתבתי בעצמי');
    expect(element.querySelector('[data-state="ai-draft"]')).toBeNull();
  });

  it('"נקה והתחל מאפס" empties the text and the mark goes', async () => {
    const element = await open();
    toReply(element);

    element.querySelector<HTMLButtonElement>('[data-action="clearDraft"]')!.click();
    harness.detectChanges();

    expect(text(element).value).toBe('');
    expect(element.querySelector('[data-state="ai-draft"]')).toBeNull();
  });

  it('the mark goes when the user deletes the text, and the suggestion does not come back', async () => {
    const element = await open();
    toReply(element);

    type(text(element), '');
    toInternal(element);
    toReply(element);

    expect(text(element).value).toBe('');
    expect(element.querySelector('[data-state="ai-draft"]')).toBeNull();
  });

  it('an unedited draft does not follow to the internal note, and does not count as unsaved text', async () => {
    const element = await open();
    toReply(element);
    const page = harness.routeDebugElement!.componentInstance as TicketHandlePage;
    expect(page.hasUnsavedChanges()).toBeFalse();

    toInternal(element);

    expect(text(element).value).toBe('');
  });

  it('an edited draft stays the user\'s text', async () => {
    const element = await open();
    toReply(element);
    type(text(element), text(element).value + ' בהצלחה');
    const page = harness.routeDebugElement!.componentInstance as TicketHandlePage;

    expect(page.hasUnsavedChanges()).toBeTrue();
    expect(element.querySelector('[data-state="ai-draft"]')).not.toBeNull();
  });

  it('adds the missing information to the text', async () => {
    const element = await open();
    toReply(element);

    element.querySelector<HTMLButtonElement>('[data-action="addMissingInfo"]')!.click();
    harness.detectChanges();

    expect(text(element).value).toBe(
      `שלום, נא לנסות להתקין מחדש את מנהל ההתקן.\n\n${MISSING_INFO_INTRO}\n- דגם המדפסת\n- הודעת השגיאה`,
    );
  });

  it('no missing-information button in the internal note', async () => {
    const element = await open();

    expect(element.querySelector('[data-action="addMissingInfo"]')).toBeNull();
  });
});
