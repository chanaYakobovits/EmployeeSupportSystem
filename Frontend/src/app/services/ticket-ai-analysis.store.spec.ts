import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AiStatus, TicketAiAnalysis } from '../models/ai-analysis.models';
import { StaffTicketDetails, TicketAction } from '../models/ticket.models';
import { fakeStaffDetails } from '../components/handler/testing/handler-fixtures';
import {
  AI_DONE_ANNOUNCEMENT,
  AI_POLL_INTERVAL_MS,
  AI_POLL_MAX_MS,
  TicketAiAnalysisStore,
} from './ticket-ai-analysis.store';
import { TicketHandleStore } from './ticket-handle.store';
import { TicketsApiService } from './tickets-api.service';

function data(status: AiStatus): TicketAiAnalysis {
  return {
    processing: { status, errorCode: null, errorMessage: null, requestedAt: null, nextAttemptAt: null },
    analysis: null,
  };
}

describe('TicketAiAnalysisStore', () => {
  let api: jasmine.SpyObj<TicketsApiService>;
  let ticket: ReturnType<typeof signal<StaffTicketDetails | null>>;
  let handle: { ticket: () => StaffTicketDetails | null; can: (a: TicketAction) => boolean; refresh: jasmine.Spy };
  let store: TicketAiAnalysisStore;

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-11-16T10:00:00Z'));
    api = jasmine.createSpyObj<TicketsApiService>('TicketsApiService', ['aiAnalysis', 'retryAiAnalysis']);
    ticket = signal<StaffTicketDetails | null>(fakeStaffDetails({ allowedActions: ['viewAiAnalysis'] }));
    const actions = computed(() => ticket()?.allowedActions ?? []);
    handle = {
      ticket: () => ticket(),
      can: (action) => actions().includes(action),
      refresh: jasmine.createSpy('refresh').and.returnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: TicketsApiService, useValue: api },
        { provide: TicketHandleStore, useValue: handle },
        TicketAiAnalysisStore,
      ],
    });
  });

  afterEach(() => jasmine.clock().uninstall());

  function create(): void {
    store = TestBed.inject(TicketAiAnalysisStore);
    TestBed.flushEffects();
  }

  it('checks a running analysis every few seconds and stops when it ends', () => {
    api.aiAnalysis.and.returnValues(of(data('Pending')), of(data('Processing')), of(data('Completed')));
    create();

    expect(api.aiAnalysis).toHaveBeenCalledTimes(1);
    expect(store.polling()).toBeTrue();

    jasmine.clock().tick(AI_POLL_INTERVAL_MS);
    expect(api.aiAnalysis).toHaveBeenCalledTimes(2);

    jasmine.clock().tick(AI_POLL_INTERVAL_MS);
    expect(api.aiAnalysis).toHaveBeenCalledTimes(3);
    expect(store.polling()).toBeFalse();
    expect(store.announcement()).toBe(AI_DONE_ANNOUNCEMENT);
    // The worker changed the ticket: its new version is loaded.
    expect(handle.refresh).toHaveBeenCalledTimes(1);

    jasmine.clock().tick(AI_POLL_INTERVAL_MS * 5);
    expect(api.aiAnalysis).toHaveBeenCalledTimes(3);
  });

  it('a finished analysis is not checked again', () => {
    api.aiAnalysis.and.returnValue(of(data('Completed')));
    create();

    jasmine.clock().tick(AI_POLL_INTERVAL_MS * 3);

    expect(api.aiAnalysis).toHaveBeenCalledTimes(1);
    expect(store.polling()).toBeFalse();
    expect(handle.refresh).not.toHaveBeenCalled();
  });

  it('gives up after a minute and offers a refresh', () => {
    api.aiAnalysis.and.returnValue(of(data('Processing')));
    create();

    for (let elapsed = 0; elapsed <= AI_POLL_MAX_MS; elapsed += AI_POLL_INTERVAL_MS) {
      jasmine.clock().tick(AI_POLL_INTERVAL_MS);
    }
    const calls = api.aiAnalysis.calls.count();
    jasmine.clock().tick(AI_POLL_INTERVAL_MS * 3);

    expect(store.stalled()).toBeTrue();
    expect(store.polling()).toBeFalse();
    expect(api.aiAnalysis.calls.count()).toBe(calls);

    store.refresh();
    expect(api.aiAnalysis.calls.count()).toBe(calls + 1);
    expect(store.polling()).toBeTrue();
  });

  it('is not loaded without viewAiAnalysis', () => {
    ticket.set(fakeStaffDetails({ allowedActions: ['reply'] }));
    create();

    expect(api.aiAnalysis).not.toHaveBeenCalled();
  });

  it('"נתח מחדש" reloads the ticket (its new version), and the analysis is checked again', () => {
    api.aiAnalysis.and.returnValues(of(data('Failed')), of(data('Pending')));
    api.retryAiAnalysis.and.returnValue(of({ id: 152, rowVersion: 'B', canView: true }));
    handle.refresh.and.callFake(() => {
      ticket.set(fakeStaffDetails({ rowVersion: 'B', allowedActions: ['viewAiAnalysis'] }));
      return of(undefined);
    });
    create();

    store.retry().subscribe();
    TestBed.flushEffects();

    expect(api.retryAiAnalysis).toHaveBeenCalledWith(152);
    expect(api.aiAnalysis).toHaveBeenCalledTimes(2);
    expect(store.polling()).toBeTrue();
  });
});
