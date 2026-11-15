import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RequesterTimelineItem } from '../../../models/ticket.models';
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

  function render(items: RequesterTimelineItem[]): HTMLElement {
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

  it('marks the resolution reply', () => {
    const element = render([fakeComment({ isResolution: true })]);
    expect(element.querySelector('.resolution-tag')!.textContent).toContain('פתרון');
  });
});
