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

  describe('staff (task 3.4)', () => {
    it('listStaff: the scope and repeated filters as query params', () => {
      service
        .listStaff({ scope: 'queue', status: ['New', 'Assigned'], urgency: ['NotSet'], overdueOnly: true, page: 1, pageSize: 25 })
        .subscribe();

      const request = http.expectOne((r) => r.url === url);
      expect(request.request.params.get('scope')).toBe('queue');
      expect(request.request.params.getAll('status')).toEqual(['New', 'Assigned']);
      expect(request.request.params.getAll('urgency')).toEqual(['NotSet']);
      expect(request.request.params.get('overdueOnly')).toBe('true');
      request.flush({ items: [], page: 1, pageSize: 25, totalCount: 0 });
    });

    it('addComment from the handling screen sends isInternal explicitly', () => {
      service.addComment(152, 'הערה', [], true).subscribe();
      let form = http.expectOne(`${url}/152/comments`).request.body as FormData;
      expect(form.get('isInternal')).toBe('true');

      service.addComment(152, 'תגובה', [], false).subscribe();
      form = http.expectOne(`${url}/152/comments`).request.body as FormData;
      expect(form.get('isInternal')).toBe('false');
    });

    it('reclassify, assign and assign-options', () => {
      service.reclassify(152, { categoryId: 3, reason: 'נימוק', rowVersion: 'AAA=' }).subscribe();
      const patch = http.expectOne(`${url}/152/classification`);
      expect(patch.request.method).toBe('PATCH');
      expect(patch.request.body).toEqual({ categoryId: 3, reason: 'נימוק', rowVersion: 'AAA=' });

      service.assign(152, { targetUserId: 14, rowVersion: 'AAA=' }).subscribe();
      const post = http.expectOne(`${url}/152/assign`);
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual({ targetUserId: 14, rowVersion: 'AAA=' });

      service.assignOptions(152).subscribe();
      expect(http.expectOne(`${url}/152/assign-options`).request.method).toBe('GET');
    });

    it('changeStatus: PATCH /status with JSON, errors shown on the screen', () => {
      service
        .changeStatus(152, { newStatus: 'Resolved', rowVersion: 'AAA=', message: 'הוחלף הטונר' })
        .subscribe();
      const request = http.expectOne(`${url}/152/status`);
      expect(request.request.method).toBe('PATCH');
      expect(request.request.body).toEqual({ newStatus: 'Resolved', rowVersion: 'AAA=', message: 'הוחלף הטונר' });
      expect(request.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    });
  });
});
