import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AuthApiService } from '../api/auth-api.service';
import { AuthService } from '../auth/auth.service';
import { PROBLEM_401, UNAUTHORIZED, fakeAuthResponse } from '../auth/testing/auth-fixtures';
import { TokenStore } from '../auth/token.store';
import { NotificationService } from '../notifications/notification.service';
import { authInterceptor } from './auth.interceptor';
import { errorInterceptor } from './error.interceptor';

const REFRESH_URL = '/api/auth/refresh';
const isRefresh = (url: string) => url === REFRESH_URL;
const isDataRequest = (url: string) => url.startsWith('/api/') && !url.startsWith('/api/auth/');

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let tokens: TokenStore;
  let router: Router;
  let notifications: jasmine.SpyObj<NotificationService>;

  beforeEach(() => {
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: NotificationService, useValue: notifications },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenStore);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    tokens.set('old-token');
  });

  afterEach(() => httpMock.verify());

  it('adds the Bearer token to API requests only', () => {
    http.get('/api/tickets').subscribe();
    http.get('https://other.example.test/data').subscribe();

    expect(httpMock.expectOne('/api/tickets').request.headers.get('Authorization')).toBe('Bearer old-token');
    expect(httpMock.expectOne('https://other.example.test/data').request.headers.has('Authorization')).toBeFalse();
  });

  it('three requests failing with 401 share ONE refresh, then each is retried once with the new token', () => {
    const completed: string[] = [];
    for (const name of ['a', 'b', 'c']) {
      http.get(`/api/${name}`).subscribe(() => completed.push(name));
    }

    const first = httpMock.match((r) => isDataRequest(r.url));
    expect(first.length).toBe(3);
    first.forEach((req) => req.flush(PROBLEM_401, UNAUTHORIZED));

    const refreshes = httpMock.match((r) => isRefresh(r.url));
    expect(refreshes.length).toBe(1);
    expect(refreshes[0].request.withCredentials).toBeTrue();
    refreshes[0].flush(fakeAuthResponse('new-token'));

    const retries = httpMock.match((r) => isDataRequest(r.url));
    expect(retries.length).toBe(3);
    retries.forEach((req) => {
      expect(req.request.headers.get('Authorization')).toBe('Bearer new-token');
      req.flush({});
    });

    expect(completed.sort()).toEqual(['a', 'b', 'c']);
    expect(tokens.token()).toBe('new-token');
    // A 401 resolved by the refresh is invisible to the user.
    expect(notifications.error).not.toHaveBeenCalled();
  });

  it('does not refresh when login fails with 401', () => {
    const api = TestBed.inject(AuthApiService);
    let failed = false;
    api.login({ identifier: 'dana', password: 'wrong' }).subscribe({ error: () => (failed = true) });

    const login = httpMock.expectOne('/api/auth/login');
    expect(login.request.withCredentials).toBeTrue();
    expect(login.request.headers.has('Authorization')).toBeFalse();
    login.flush(PROBLEM_401, UNAUTHORIZED);

    httpMock.expectNone(REFRESH_URL);
    expect(failed).toBeTrue();
    expect(notifications.error).not.toHaveBeenCalled(); // the login screen shows the error itself
  });

  it('does not refresh again when the refresh request itself fails with 401', () => {
    let failed = false;
    TestBed.inject(AuthService).refresh().subscribe({ error: () => (failed = true) });

    httpMock.expectOne(REFRESH_URL).flush(PROBLEM_401, UNAUTHORIZED);

    httpMock.expectNone(REFRESH_URL);
    expect(failed).toBeTrue();
  });

  it('does not refresh on 401 from logout', () => {
    let failed = false;
    TestBed.inject(AuthApiService).logout().subscribe({ error: () => (failed = true) });

    httpMock.expectOne('/api/auth/logout').flush(PROBLEM_401, UNAUTHORIZED);

    httpMock.expectNone(REFRESH_URL);
    expect(failed).toBeTrue();
  });

  it('when the refresh fails: signs out locally and opens the login screen with returnUrl', () => {
    spyOnProperty(router, 'url').and.returnValue('/handler?page=2');
    let status = 0;
    http.get('/api/tickets').subscribe({ error: (e: { status: number }) => (status = e.status) });

    httpMock.expectOne('/api/tickets').flush(PROBLEM_401, UNAUTHORIZED);
    httpMock.expectOne(REFRESH_URL).flush(PROBLEM_401, UNAUTHORIZED);

    expect(tokens.token()).toBeNull();
    expect(TestBed.inject(AuthService).isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledOnceWith(['/login'], { queryParams: { returnUrl: '/handler?page=2' } });
    expect(status).toBe(401);
    expect(notifications.error).not.toHaveBeenCalled();
  });

  it('retries only once: a second 401 after a successful refresh is an error (with a toast)', () => {
    let status = 0;
    http.get('/api/tickets').subscribe({ error: (e: { status: number }) => (status = e.status) });

    httpMock.expectOne('/api/tickets').flush(PROBLEM_401, UNAUTHORIZED);
    httpMock.expectOne(REFRESH_URL).flush(fakeAuthResponse('new-token'));
    httpMock.expectOne('/api/tickets').flush(PROBLEM_401, UNAUTHORIZED);

    httpMock.expectNone(REFRESH_URL);
    expect(status).toBe(401);
    expect(notifications.error).toHaveBeenCalledTimes(1);
  });

  it('passes other errors through without refreshing', () => {
    let status = 0;
    http.get('/api/tickets').subscribe({ error: (e: { status: number }) => (status = e.status) });

    httpMock.expectOne('/api/tickets').flush({ title: 'אין הרשאה', status: 403 }, { status: 403, statusText: 'Forbidden' });

    httpMock.expectNone(REFRESH_URL);
    expect(status).toBe(403);
    expect(notifications.error).toHaveBeenCalledOnceWith('אין הרשאה');
  });
});
