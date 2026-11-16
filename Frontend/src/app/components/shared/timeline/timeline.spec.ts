import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RequesterTimelineItem, StaffTimelineItem } from '../../../models/ticket.models';
import { AttachmentDownloadService } from '../../../services/attachment-download.service';
import { fakeAttachment, fakeComment } from '../../employee/testing/ticket-fixtures';
import { Timeline } from './timeline';
import { EMPTY } from 'rxjs';

describe('Timeline', () => {
  let fixture: ComponentFixture<Timeline>;
  let downloads: jasmine.SpyObj<AttachmentDownloadService>;

  beforeEach(() => {
    downloads = jasmine.createSpyObj<AttachmentDownloadService>('AttachmentDownloadService', ['download']);
    downloads.download.and.returnValue(EMPTY);
    TestBed.configureTestingModule({ providers: [{ provide: AttachmentDownloadService, useValue: downloads }] });
    fixture = TestBed.createComponent(Timeline);
    fixture.componentRef.setInput('ticketId', 1042);
  });

  function render(items: (RequesterTimelineItem | StaffTimelineItem)[]): HTMLElement {
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  it('an ordered list with every kind of entry, in the order received', () => {
    const element = render([
      { kind: 'status', at: '2026-11-10T08:00:00Z', previousStatus: null, newStatus: 'New', changedByName: 'ישראל ישראלי' },
      { kind: 'attachment', at: '2026-11-10T08:00:00Z', attachment: fakeAttachment({ fileName: 'תלוש.pdf' }) },
      { kind: 'status', at: '2026-11-10T08:01:00Z', previousStatus: 'New', newStatus: 'Assigned', changedByName: null },
      fakeComment({ id: 1, body: 'אפשר צילום?' }),
    ]);

    const list = element.querySelector('ol')!;
    const entries = Array.from(list.children).map((li) => li.textContent!);
    expect(list.querySelectorAll(':scope > li').length).toBe(4);
    expect(entries[0]).toContain('הפנייה נפתחה');
    expect(entries[0]).toContain('ישראל ישראלי');
    expect(entries[1]).toContain('צירף/ה קובץ');
    expect(entries[1]).toContain('תלוש.pdf');
    expect(entries[2]).toContain('שויכה');
    expect(entries[2]).toContain('מערכת');
    expect(entries[3]).toContain('אפשר צילום?');
  });

  it('the employee\'s comments and the handler\'s look different', () => {
    const element = render([
      fakeComment({ id: 1, isFromRequester: false, authorName: 'דנה כהן' }),
      fakeComment({ id: 2, isFromRequester: true, authorName: 'ישראל ישראלי' }),
    ]);
    const [staff, mine] = Array.from(element.querySelectorAll<HTMLElement>('.entry-comment'));
    expect(staff.classList).toContain('from-staff');
    expect(mine.classList).toContain('from-me');
    expect(staff.textContent).toContain('תגובה מ-דנה כהן');
    expect(mine.textContent).toContain('תגובה שלך');
    expect(getComputedStyle(staff.querySelector('.bubble')!).backgroundColor).not.toBe(
      getComputedStyle(mine.querySelector('.bubble')!).backgroundColor,
    );
  });

  it('a comment is plain text, not HTML, with its line breaks and no links', () => {
    const body = '<b>מודגש</b><img src="x" onerror="alert(1)">\nשורה שנייה https://example.com';
    const element = render([fakeComment({ body })]);
    const shown = element.querySelector<HTMLElement>('.body')!;
    expect(shown.querySelector('b')).toBeNull();
    expect(shown.querySelector('img')).toBeNull();
    expect(shown.querySelector('a')).toBeNull();
    expect(shown.textContent).toBe(body);
    expect(getComputedStyle(shown).whiteSpace).toBe('pre-wrap');
  });

  it('a comment\'s files as chips that download on click', () => {
    const attachment = fakeAttachment({ id: 9, fileName: 'צילום.png' });
    const element = render([fakeComment({ attachments: [attachment] })]);
    const chip = element.querySelector<HTMLButtonElement>('app-attachment-chip button')!;
    expect(chip.getAttribute('aria-label')).toBe('הורדת הקובץ צילום.png, 350 KB');
    chip.click();
    expect(downloads.download).toHaveBeenCalledOnceWith(1042, attachment);
  });

  it('the resolution reply stands out: the "פתרון" tag, its own look and a label for screen readers', () => {
    const element = render([
      fakeComment({ id: 1, isResolution: true, authorName: 'דנה כהן' }),
      fakeComment({ id: 2, isResolution: false }),
    ]);
    const [resolution, plain] = Array.from(element.querySelectorAll<HTMLElement>('.entry-comment'));
    expect(resolution.classList).toContain('resolution');
    expect(resolution.querySelector('.resolution-tag')!.textContent).toContain('פתרון');
    expect(resolution.textContent).toContain('תגובת פתרון מ-דנה כהן');
    expect(plain.classList).not.toContain('resolution');
    expect(plain.querySelector('.resolution-tag')).toBeNull();
    expect(getComputedStyle(resolution.querySelector('.bubble')!).backgroundColor).not.toBe(
      getComputedStyle(plain.querySelector('.bubble')!).backgroundColor,
    );
  });

  it('system events: the "מערכת" tag and what happened, in words', () => {
    const at = '2026-11-10T08:00:00Z';
    const element = render([
      { kind: 'status', at, previousStatus: null, newStatus: 'New', changedByName: 'ישראל ישראלי' },
      { kind: 'status', at, previousStatus: 'New', newStatus: 'Assigned', changedByName: null },
      { kind: 'status', at, previousStatus: 'WaitingForEmployee', newStatus: 'InProgress', changedByName: null },
      { kind: 'status', at, previousStatus: 'Resolved', newStatus: 'Closed', changedByName: null },
      { kind: 'status', at, previousStatus: 'Assigned', newStatus: 'InProgress', changedByName: 'דנה כהן' },
    ]);
    const entries = Array.from(element.querySelectorAll<HTMLElement>('.entry-status'));

    expect(entries[0].querySelector('.system-tag')).toBeNull();
    expect(entries[1].querySelector('.system-tag')!.textContent).toContain('מערכת');
    expect(entries[1].textContent).toContain('הפנייה שויכה לטיפול');
    expect(entries[2].textContent).toContain('הפנייה חזרה לטיפול אחרי התגובה שלך');
    expect(entries[3].textContent).toContain('הפנייה נסגרה אוטומטית 7 ימים אחרי הפתרון');
    expect(entries[4].querySelector('.system-tag')).toBeNull();
    expect(entries[4].textContent).toContain('דנה כהן');
  });

  it('system events from the staff perspective speak of the employee', () => {
    fixture.componentRef.setInput('perspective', 'staff');
    const element = render([
      {
        kind: 'status',
        at: '2026-11-10T08:00:00Z',
        previousStatus: 'WaitingForEmployee',
        newStatus: 'InProgress',
        changedByName: null,
        reason: null,
      },
    ]);
    expect(element.textContent).toContain('הפנייה חזרה לטיפול אחרי תגובת העובד');
  });

  describe('staff view (task 3.4)', () => {
    it('an internal note is yellow with a "פנימי" tag and says so to screen readers', () => {
      fixture.componentRef.setInput('perspective', 'staff');
      const element = render([
        { ...fakeComment({ id: 1, body: 'גלוי' }), isInternal: false },
        { ...fakeComment({ id: 2, body: 'פנימי בלבד', authorName: 'אלון' }), isInternal: true },
      ]);
      const [visible, internal] = Array.from(element.querySelectorAll<HTMLElement>('.entry-comment'));
      expect(visible.classList).not.toContain('internal');
      expect(visible.querySelector('.internal-tag')).toBeNull();
      expect(internal.classList).toContain('internal');
      expect(internal.querySelector('.internal-tag')!.textContent).toContain('פנימי');
      expect(internal.textContent).toContain('הערה פנימית מ-אלון');
      expect(getComputedStyle(internal.querySelector('.bubble')!).backgroundColor).not.toBe(
        getComputedStyle(visible.querySelector('.bubble')!).backgroundColor,
      );
    });

    it("the requester's comment is the employee's for staff", () => {
      fixture.componentRef.setInput('perspective', 'staff');
      const element = render([{ ...fakeComment({ isFromRequester: true, authorName: 'נועה' }), isInternal: false }]);
      expect(element.textContent).toContain('תגובה מהעובד נועה');
      expect(element.textContent).not.toContain('תגובה שלך');
    });

    it('shows the reason of a status change', () => {
      const element = render([
        {
          kind: 'status',
          at: '2026-11-10T08:00:00Z',
          previousStatus: 'InProgress',
          newStatus: 'Assigned',
          changedByName: 'אלון',
          reason: 'הועבר למטפלת אחרת',
        },
      ]);
      expect(element.querySelector('.reason')!.textContent).toContain('הועבר למטפלת אחרת');
    });

    it('a reclassification / transfer shows only what changed, and its reason', () => {
      const element = render([
        {
          kind: 'change',
          at: '2026-11-10T09:00:00Z',
          changeKind: 'Transferred',
          changedByName: 'אלון ביטון',
          reason: 'עומס',
          fromCategoryName: 'מחשוב',
          toCategoryName: 'מחשוב',
          fromUrgency: null,
          toUrgency: 'High',
          fromDepartmentName: 'פיתוח',
          toDepartmentName: 'משאבי אנוש',
          fromHandlerName: 'אלון ביטון',
          toHandlerName: null,
        },
      ]);
      const entry = element.querySelector('.entry-change')!;
      const lines = Array.from(entry.querySelectorAll('.change-lines li')).map((li) => li.textContent!.replace(/\s+/g, ' '));
      expect(entry.textContent).toContain('הפנייה הועברה');
      expect(lines.length).toBe(3);
      expect(lines[0]).toContain('לא נקבעה');
      expect(lines[0]).toContain('גבוהה');
      expect(lines[1]).toContain('משאבי אנוש');
      expect(lines[2]).toContain('תור המחלקה');
      expect(entry.querySelector('.reason')!.textContent).toContain('עומס');
    });
  });
});
