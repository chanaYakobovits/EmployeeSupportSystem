import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { fakeAttachment } from '../components/employee/testing/ticket-fixtures';
import {
  AttachmentDownloadService,
  DOWNLOAD_NOT_FOUND_MESSAGE,
  fileNameFromContentDisposition,
} from './attachment-download.service';
import { SKIP_ERROR_TOAST } from './http-context';
import { NotificationService } from './notification.service';

describe('fileNameFromContentDisposition', () => {
  it('reads the Hebrew name from filename* (ASP.NET Core format)', () => {
    const header =
      "attachment; filename=\"___ ____.png\"; filename*=UTF-8''%D7%A6%D7%99%D7%9C%D7%95%D7%9D%20%D7%9E%D7%A1%D7%9A.png";
    expect(fileNameFromContentDisposition(header)).toBe('צילום מסך.png');
  });

  it('filename* wins even when it comes first', () => {
    expect(fileNameFromContentDisposition("attachment; filename*=utf-8''%D7%90.pdf; filename=a.pdf")).toBe('א.pdf');
  });

  it('falls back to filename (quoted or not)', () => {
    expect(fileNameFromContentDisposition('attachment; filename="report 1.pdf"')).toBe('report 1.pdf');
    expect(fileNameFromContentDisposition('attachment; filename=report.pdf')).toBe('report.pdf');
  });

  it('malformed filename* → filename', () => {
    expect(fileNameFromContentDisposition("attachment; filename=a.pdf; filename*=UTF-8''%E0%A4%A.pdf")).toBe('a.pdf');
  });

  it('no header or no name → null', () => {
    expect(fileNameFromContentDisposition(null)).toBeNull();
    expect(fileNameFromContentDisposition('attachment')).toBeNull();
  });
});

describe('AttachmentDownloadService', () => {
  const url = `${environment.apiBaseUrl}/tickets/1042/attachments/7`;
  let service: AttachmentDownloadService;
  let http: HttpTestingController;
  let notifications: jasmine.SpyObj<NotificationService>;
  let clicked: HTMLAnchorElement[];

  beforeEach(() => {
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: NotificationService, useValue: notifications },
      ],
    });
    service = TestBed.inject(AttachmentDownloadService);
    http = TestBed.inject(HttpTestingController);
    clicked = [];
    spyOn(URL, 'createObjectURL').and.returnValue('blob:http://localhost/abc');
    spyOn(URL, 'revokeObjectURL');
    // No real download in the test browser: record the link instead.
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (this: HTMLAnchorElement) {
      clicked.push(this);
    });
  });

  afterEach(() => http.verify());

  it('fetches the file as a blob through HttpClient (so the token is sent), without a toast from the interceptor', () => {
    service.download(1042, fakeAttachment()).subscribe();
    const request = http.expectOne(url);
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    expect(request.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    request.flush(new Blob(['x']));
  });

  it('saves under the name from Content-Disposition and releases the object URL right after', () => {
    let completed = false;
    service.download(1042, fakeAttachment()).subscribe({ complete: () => (completed = true) });
    http.expectOne(url).flush(new Blob(['x']), {
      headers: { 'Content-Disposition': "attachment; filename=x.pdf; filename*=UTF-8''%D7%AA%D7%9C%D7%95%D7%A9.pdf" },
    });

    expect(clicked.length).toBe(1);
    expect(clicked[0].download).toBe('תלוש.pdf');
    expect(clicked[0].href).toBe('blob:http://localhost/abc');
    expect(clicked[0].isConnected).toBeFalse();
    expect(URL.revokeObjectURL).toHaveBeenCalledOnceWith('blob:http://localhost/abc');
    expect(completed).toBeTrue();
  });

  it('without the header: the name from the timeline', () => {
    service.download(1042, fakeAttachment({ fileName: 'צילום מסך.png' })).subscribe();
    http.expectOne(url).flush(new Blob(['x']));
    expect(clicked[0].download).toBe('צילום מסך.png');
  });

  it('404: a Hebrew message, nothing is saved, and the stream completes', () => {
    let completed = false;
    service.download(1042, fakeAttachment()).subscribe({ complete: () => (completed = true) });
    http.expectOne(url).flush(new Blob(), { status: 404, statusText: 'Not Found' });

    expect(notifications.error).toHaveBeenCalledOnceWith(DOWNLOAD_NOT_FOUND_MESSAGE);
    expect(clicked.length).toBe(0);
    expect(URL.createObjectURL).not.toHaveBeenCalled();
    expect(completed).toBeTrue();
  });

  it('another error: the general message for its status', () => {
    service.download(1042, fakeAttachment()).subscribe();
    http.expectOne(url).flush(new Blob(), { status: 500, statusText: 'Server Error' });
    expect(notifications.error).toHaveBeenCalledOnceWith('אירעה שגיאה בלתי צפויה. יש לנסות שוב מאוחר יותר.');
  });
});
