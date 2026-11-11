import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { authInterceptor } from '../interceptors/auth.interceptor';
import { errorInterceptor } from '../interceptors/error.interceptor';
import { NotificationService } from '../notifications/notification.service';
import { AuthService } from './auth.service';
import { PROBLEM_401, UNAUTHORIZED, fakeAuthResponse } from './testing/auth-fixtures';
import { TokenStore } from './token.store';

describe('AuthService', () => {
  let auth: AuthService;
  let tokens: TokenStore;
  let httpMock: HttpTestingController;
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
    auth = TestBed.inject(AuthService);
    tokens = TestBed.inject(TokenStore);
    httpMock = TestBed.inject(HttpTestingController);
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => httpMock.verify());

  function storageContains(value: string): boolean {
    for (const storage of [localStorage, sessionStorage]) {
      for (let i = 0; i < storage.length; i++) {
        const key = storage.key(i)!;
        if (key.includes(value) || (storage.getItem(key) ?? '').includes(value)) {
          return true;
        }
      }
    }
    return false;
  }

  it('keeps the access token in memory only – never in localStorage / sessionStorage', () => {
    const setItem = spyOn(Storage.prototype, 'setItem').and.callThrough();

    auth.login({ identifier: 'dana@example.test', password: 'Secret123' }).subscribe();
    httpMock.expectOne('/api/auth/login').flush(fakeAuthResponse('secret-access-token'));

    auth.refresh().subscribe();
    httpMock.expectOne('/api/auth/refresh').flush(fakeAuthResponse('second-access-token'));

    expect(tokens.token()).toBe('second-access-token');
    expect(setItem).not.toHaveBeenCalled();
    expect(storageContains('secret-access-token')).toBeFalse();
    expect(storageContains('second-access-token')).toBeFalse();
  });

  it('exposes the session as signals after login', () => {
    auth.login({ identifier: 'E1007', password: 'Temp1234' }).subscribe();
    httpMock.expectOne('/api/auth/login').flush(fakeAuthResponse('t', true));

    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.mustChangePassword()).toBeTrue();
    expect(auth.roles()).toEqual(['Employee']);
    expect(auth.currentUser()?.firstName).toBe('דנה');
  });

  it('restoreSession: a failed refresh leaves the user signed out, without an error message', async () => {
    const restored = auth.restoreSession();
    httpMock.expectOne('/api/auth/refresh').flush(PROBLEM_401, UNAUTHORIZED);

    await expectAsync(restored).toBeResolved();
    expect(auth.isAuthenticated()).toBeFalse();
    expect(notifications.error).not.toHaveBeenCalled();
  });

  it('restoreSession: a valid refresh cookie restores the session', async () => {
    const restored = auth.restoreSession();
    httpMock.expectOne('/api/auth/refresh').flush(fakeAuthResponse('restored'));

    await restored;
    expect(auth.isAuthenticated()).toBeTrue();
    expect(tokens.token()).toBe('restored');
  });

  it('logout clears the session even when the server call fails, and opens the login screen', async () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    auth.login({ identifier: 'dana', password: 'Secret123' }).subscribe();
    httpMock.expectOne('/api/auth/login').flush(fakeAuthResponse());

    const done = auth.logout();
    httpMock.expectOne('/api/auth/logout').flush(null, { status: 500, statusText: 'Server Error' });
    await done;

    expect(auth.isAuthenticated()).toBeFalse();
    expect(tokens.token()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
    expect(notifications.error).not.toHaveBeenCalled();
  });
});
