import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TicketStatus } from '../../../models/ticket.models';
import { ClockService } from '../../../services/clock.service';
import { DueCountdown } from './due-countdown';

describe('DueCountdown', () => {
  const start = new Date('2026-11-16T10:00:00Z');
  const now = signal(start);
  let fixture: ComponentFixture<DueCountdown>;

  beforeEach(() => {
    now.set(start);
    TestBed.configureTestingModule({ providers: [{ provide: ClockService, useValue: { now: now.asReadonly() } }] });
    fixture = TestBed.createComponent(DueCountdown);
  });

  function render(dueAt: string | null, status: TicketStatus = 'InProgress', isOverdue = false): HTMLElement {
    fixture.componentRef.setInput('dueAt', dueAt);
    fixture.componentRef.setInput('status', status);
    fixture.componentRef.setInput('isOverdue', isOverdue);
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  it('"נותרו 3 שעות" with the due date for screen readers', () => {
    const element = render('2026-11-16T13:00:00Z');
    expect(element.textContent).toContain('נותרו 3 שעות');
    expect(element.textContent).toContain('16/11/2026 15:00');
    expect(element.classList).not.toContain('overdue');
  });

  it('overdue: red, with a warning icon and "חריגה של שעתיים"', () => {
    const element = render('2026-11-16T08:00:00Z', 'Assigned', true);
    expect(element.classList).toContain('overdue');
    expect(element.textContent).toContain('חריגה של שעתיים');
    expect(element.querySelector('mat-icon')!.textContent).toContain('warning');
  });

  it('moves with the clock: once the due time passes it turns overdue', () => {
    const element = render('2026-11-16T10:30:00Z');
    expect(element.textContent).toContain('נותרו 30 דקות');

    now.set(new Date('2026-11-16T10:31:00Z'));
    fixture.detectChanges();
    expect(element.textContent).toContain('חריגה של דקה');
    expect(element.classList).toContain('overdue');
  });

  it('not shown while waiting for the employee or once closed', () => {
    expect(render('2026-11-16T08:00:00Z', 'WaitingForEmployee', true).textContent!.trim()).toBe('');
    expect(render('2026-11-16T08:00:00Z', 'Closed').textContent!.trim()).toBe('');
  });

  it('no due time: "—"', () => {
    expect(render(null, 'New').textContent).toContain('—');
  });
});
