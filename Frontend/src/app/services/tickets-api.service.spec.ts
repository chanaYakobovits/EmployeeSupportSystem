import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { fakeFile } from '../components/employee/testing/ticket-fixtures';
import { SKIP_ERROR_TOAST } from './http-context';
import { TicketsApiService } from './tickets-api.service';

describe('TicketsApiService', () => {
  const url = `${environment.apiBaseUrl}/tickets`;
  let service: TicketsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TicketsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('create', () => {
    it('posts the fields as multipart form data, not JSON', () => {
      let created: number | undefined;
      service.create({ subject: 'תלוש אוקטובר', description: 'חסרות 6 שעות נוספות', files: [] }).subscribe((r) => (created = r.id));

      const request = http.expectOne(url);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toBeInstanceOf(FormData);
      const form = request.request.body as FormData;
      expect(form.get('subject')).toBe('תלוש אוקטובר');
      expect(form.get('description')).toBe('חסרות 6 שעות נוספות');
      expect([...form.keys()]).toEqual(['subject', 'description']);

      request.flush({ id: 1042 });
      expect(created).toBe(1042);
    });

    it('leaves the Content-Type to the browser (it adds the multipart boundary)', () => {
      service.create({ subject: null, description: 'המדפסת לא מדפיסה', files: [] }).subscribe();

      const request = http.expectOne(url);
      expect(request.request.headers.has('Content-Type')).toBeFalse();
      request.flush({ id: 1 });
    });

    it('does not send a missing subject', () => {
      service.create({ subject: null, description: 'המדפסת לא מדפיסה', files: [] }).subscribe();

      const form = http.expectOne(url).request.body as FormData;
      expect(form.has('subject')).toBeFalse();
      expect(form.get('description')).toBe('המדפסת לא מדפיסה');
    });

    it('shows errors on the screen, not as a toast', () => {
      service.create({ subject: null, description: 'המדפסת לא מדפיסה', files: [] }).subscribe();

      expect(http.expectOne(url).request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    });

    it('sends every file under "files", with its name, after the text fields', () => {
      const files = [fakeFile('תלוש.pdf'), fakeFile('צילום.png')];
      service.create({ subject: 'תלוש', description: 'חסרות 6 שעות נוספות', files }).subscribe();

      const form = http.expectOne(url).request.body as FormData;
      expect([...form.keys()]).toEqual(['subject', 'description', 'files', 'files']);
      expect(form.get('subject')).toBe('תלוש');
      expect(form.get('description')).toBe('חסרות 6 שעות נוספות');
      const sent = form.getAll('files') as File[];
      expect(sent.map((f) => f.name)).toEqual(['תלוש.pdf', 'צילום.png']);
      expect(sent.map((f) => f.size)).toEqual([1024, 1024]);
    });
  });

  it('history: GET /tickets/{id}/history', () => {
    service.history(1042).subscribe();
    const request = http.expectOne(`${url}/1042/history`);
    expect(request.request.method).toBe('GET');
    expect(request.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
  });

  it('addComment: multipart body and files, never isInternal', () => {
    service.addComment(1042, 'הנה הצילום', [fakeFile('צילום.png')]).subscribe();

    const request = http.expectOne(`${url}/1042/comments`);
    expect(request.request.method).toBe('POST');
    const form = request.request.body as FormData;
    expect([...form.keys()]).toEqual(['body', 'files']);
    expect(form.get('body')).toBe('הנה הצילום');
    expect((form.get('files') as File).name).toBe('צילום.png');
    expect(request.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
  });

  it('addAttachments: multipart files only', () => {
    service.addAttachments(1042, [fakeFile('a.pdf'), fakeFile('b.docx')]).subscribe();

    const request = http.expectOne(`${url}/1042/attachments`);
    expect(request.request.method).toBe('POST');
    const form = request.request.body as FormData;
    expect([...form.keys()]).toEqual(['files', 'files']);
  });
});
