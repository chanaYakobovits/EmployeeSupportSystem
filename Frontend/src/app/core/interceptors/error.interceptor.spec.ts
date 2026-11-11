import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { skipErrorToast } from '../http/http-context';
import { NotificationService } from '../notifications/notification.service';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let notifications: jasmine.SpyObj<NotificationService>;

  beforeEach(() => {
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: NotificationService, useValue: notifications },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  const problem = { title: 'הבקשה אינה תקינה', status: 400 };

  it('shows the ProblemDetails title in a toast', () => {
    http.get('/api/x').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/x').flush(problem, { status: 400, statusText: 'Bad Request' });

    expect(notifications.error).toHaveBeenCalledOnceWith('הבקשה אינה תקינה');
  });

  it('stays silent when the request opted out with SKIP_ERROR_TOAST, and still re-throws', () => {
    let failed = false;
    http.get('/api/x', { context: skipErrorToast() }).subscribe({ error: () => (failed = true) });
    httpMock.expectOne('/api/x').flush(problem, { status: 400, statusText: 'Bad Request' });

    expect(notifications.error).not.toHaveBeenCalled();
    expect(failed).toBeTrue();
  });
});
