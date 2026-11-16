import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';

import { BulkAssignOptions } from '../../../../models/ticket.models';
import { TicketQueueStore } from '../../../../services/ticket-queue.store';
import { BulkAssignDialog } from './bulk-assign-dialog';

describe('BulkAssignDialog', () => {
  const options: BulkAssignOptions = {
    handlers: [{ id: 9, name: 'דנה לוי', openTicketCount: 4 }],
    departments: [{ id: 11, name: 'משאבי אנוש' }],
  };
  let store: { selectedCount: () => number; bulkBusy: ReturnType<typeof signal<boolean>>; bulkAssignOptions: jasmine.Spy; bulkAssign: jasmine.Spy };
  let dialogRef: jasmine.SpyObj<MatDialogRef<BulkAssignDialog>>;
  let fixture: ComponentFixture<BulkAssignDialog>;
  let element: HTMLElement;

  beforeEach(() => {
    store = {
      selectedCount: () => 3,
      bulkBusy: signal(false),
      bulkAssignOptions: jasmine.createSpy('bulkAssignOptions').and.returnValue(of(options)),
      bulkAssign: jasmine.createSpy('bulkAssign'),
    };
    dialogRef = jasmine.createSpyObj<MatDialogRef<BulkAssignDialog>>('MatDialogRef', ['close']);
    TestBed.configureTestingModule({
      imports: [BulkAssignDialog],
      providers: [
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: TicketQueueStore, useValue: store },
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    });
    fixture = TestBed.createComponent(BulkAssignDialog);
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
  });

  function chooseHandler(): void {
    element.querySelector<HTMLElement>('mat-select[formControlName="userId"] .mat-mdc-select-trigger')!.click();
    fixture.detectChanges();
    document.querySelector<HTMLElement>('mat-option')!.click();
    fixture.detectChanges();
  }

  function typeReason(text: string): void {
    const textarea = element.querySelector<HTMLTextAreaElement>('textarea')!;
    textarea.value = text;
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function submit(): void {
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    fixture.detectChanges();
  }

  it('says how many tickets move', () => {
    expect(element.querySelector('h2')!.textContent).toContain('העברת 3 פניות');
  });

  it('a target and a reason are required; nothing is sent without them', () => {
    submit();

    expect(store.bulkAssign).not.toHaveBeenCalled();
    expect(element.textContent).toContain('יש לבחור מטפל');
    expect(element.textContent).toContain('יש לכתוב נימוק');
  });

  it('sends the handler and the trimmed reason, and closes with the result', () => {
    const result = { succeeded: [1, 2], failed: [{ id: 3, reason: 'InvalidTarget', errorType: 'assignee-is-requester' }] };
    store.bulkAssign.and.returnValue(of(result));
    chooseHandler();
    typeReason('  עומס במחלקה  ');

    submit();

    expect(store.bulkAssign).toHaveBeenCalledWith({ targetUserId: 9 }, 'עומס במחלקה');
    expect(dialogRef.close).toHaveBeenCalledWith(result);
  });

  it('a 400 stays open and shows the error on its field', () => {
    store.bulkAssign.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { title: 'הבקשה אינה תקינה', status: 400, errors: { reason: ['נימוק: בין 3 ל-500 תווים'] } },
          }),
      ),
    );
    chooseHandler();
    typeReason('קצר מדי?');

    submit();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(element.textContent).toContain('נימוק: בין 3 ל-500 תווים');
  });
});
