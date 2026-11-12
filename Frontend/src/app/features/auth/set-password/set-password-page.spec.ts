import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Subject, of, throwError } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { problemResponse } from '../../admin/testing/admin-fixtures';
import { readLoginNotice } from '../login-notices';
import { INVALID_LINK_MESSAGE, SetPasswordPage, readToken } from './set-password-page';

@Component({ template: 'login' })
class LoginStub {}

const TOKEN = 'Abc-123_xyzTOKENvalue';
const PASSWORD = 'NewPass123';

describe('SetPasswordPage', () => {
  let auth: { isAuthenticated: ReturnType<typeof signal<boolean>>; signOut: jasmine.Spy; setPassword: jasmine.Spy };
  let harness: RouterTestingHarness;
  let element: HTMLElement;

  beforeEach(() => {
    auth = {
      isAuthenticated: signal(false),
      signOut: jasmine.createSpy('signOut').and.resolveTo(undefined),
      setPassword: jasmine.createSpy('setPassword').and.returnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'set-password', component: SetPasswordPage },
          { path: 'login', component: LoginStub },
        ]),
        provideLocationMocks(),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: AuthService, useValue: auth },
      ],
    });
  });

  async function open(url: string): Promise<void> {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    harness.detectChanges();
    element = harness.routeNativeElement!;
  }

  function type(name: string, value: string): void {
    const input = element.querySelector(`input[formcontrolname="${name}"]`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function submitValid(): void {
    type('newPassword', PASSWORD);
    type('confirmPassword', PASSWORD);
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    harness.detectChanges();
  }

  function referrerMeta(): HTMLMetaElement | null {
    return TestBed.inject(DOCUMENT).head.querySelector('meta[name="referrer"]');
  }

  describe('with a token in the fragment', () => {
    beforeEach(async () => open(`/set-password#token=${TOKEN}`));

    it('reads the token from the fragment and removes it from the address bar at once', () => {
      expect(TestBed.inject(Location).path(true)).toBe('/set-password');
      expect(element.querySelector('form')).not.toBeNull();
    });

    it('sends the token from the fragment with the new password', () => {
      submitValid();
      expect(auth.setPassword).toHaveBeenCalledOnceWith({ token: TOKEN, newPassword: PASSWORD });
    });

    it('never puts the token in the page', () => {
      submitValid();
      expect(element.innerHTML).not.toContain(TOKEN);
    });

    it('uses the shared new-password fields with the policy rules', () => {
      expect(element.querySelector('app-new-password-fields')).not.toBeNull();
      expect(element.querySelectorAll('.rules li').length).toBe(3);
      expect(element.querySelector('input[formcontrolname="newPassword"]')!.getAttribute('autocomplete')).toBe(
        'new-password',
      );
    });

    it('an empty submit shows the field errors and focuses the first field', () => {
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      harness.detectChanges();

      expect(auth.setPassword).not.toHaveBeenCalled();
      expect(element.textContent).toContain('יש להזין סיסמה חדשה');
      expect(element.textContent).toContain('יש להזין שוב את הסיסמה החדשה');
      expect(document.activeElement?.getAttribute('formcontrolname')).toBe('newPassword');
    });

    it('checks the policy before calling the server', () => {
      type('newPassword', 'short');
      type('confirmPassword', 'short');
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      harness.detectChanges();

      expect(auth.setPassword).not.toHaveBeenCalled();
      expect(element.textContent).toContain('הסיסמה אינה עומדת בדרישות');
    });

    it('success → the login screen with the notice (in the navigation state, not the URL)', async () => {
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.callThrough();

      submitValid();
      await harness.fixture.whenStable();

      const [commands, extras] = navigate.calls.mostRecent().args;
      expect(commands).toEqual(['/login']);
      expect(readLoginNotice(extras?.state)).toBe('הסיסמה נקבעה. אפשר להתחבר עם המייל או קוד העובד.');
      expect(router.url).toBe('/login');
    });

    it('an invalid link from the server → the uniform message instead of the form', () => {
      auth.setPassword.and.returnValue(
        throwError(() => problemResponse(400, 'invalid-invitation', 'הקישור אינו בתוקף. יש לפנות למנהל המערכת.')),
      );

      submitValid();

      expect(element.querySelector('form')).toBeNull();
      expect(element.textContent).toContain(INVALID_LINK_MESSAGE);
      expect(element.querySelector('a[href="/login"]')).not.toBeNull();
    });

    it('a temporary error (429) keeps the form and the token, so the employee can try again', () => {
      auth.setPassword.and.returnValue(throwError(() => new HttpErrorResponse({ status: 429 })));
      submitValid();

      expect(element.querySelector('form')).not.toBeNull();
      expect(element.querySelector('.auth-alert')).not.toBeNull();

      auth.setPassword.and.returnValue(of(undefined));
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      expect(auth.setPassword.calls.mostRecent().args[0]).toEqual({ token: TOKEN, newPassword: PASSWORD });
    });

    it('a policy error from the server is shown on the field', () => {
      auth.setPassword.and.returnValue(
        throwError(() =>
          problemResponse(400, 'password-policy', 'הסיסמה אינה עומדת בדרישות', {
            errors: { newPassword: ['הסיסמה חייבת להכיל לפחות ספרה אחת'] },
          }),
        ),
      );
      submitValid();

      const field = element.querySelector('input[formcontrolname="newPassword"]')!.closest('mat-form-field')!;
      expect(field.textContent).toContain('הסיסמה חייבת להכיל לפחות ספרה אחת');
    });

    it('sets Referrer-Policy no-referrer while open, and removes it on leave', async () => {
      expect(referrerMeta()?.content).toBe('no-referrer');

      await harness.navigateByUrl('/login');

      expect(referrerMeta()).toBeNull();
    });

    it('does not submit twice while a request runs', () => {
      const pending = new Subject<void>();
      auth.setPassword.and.returnValue(pending);
      submitValid();
      element.querySelector('form')!.dispatchEvent(new Event('submit'));

      expect(auth.setPassword).toHaveBeenCalledTimes(1);
    });
  });

  describe('without a token', () => {
    for (const url of ['/set-password', '/set-password#token=', '/set-password#other=1']) {
      it(`${url} → the uniform message and a link to login, no form and no request`, async () => {
        await open(url);

        expect(element.textContent).toContain(INVALID_LINK_MESSAGE);
        expect(element.querySelector('form')).toBeNull();
        expect(element.querySelector('a[href="/login"]')).not.toBeNull();
        expect(auth.setPassword).not.toHaveBeenCalled();
      });
    }
  });

  it('a signed-in user is signed out first (the link belongs to another account)', async () => {
    auth.isAuthenticated.set(true);
    await open(`/set-password#token=${TOKEN}`);

    expect(auth.signOut).toHaveBeenCalledTimes(1);
  });

  it('a guest is not signed out', async () => {
    await open(`/set-password#token=${TOKEN}`);

    expect(auth.signOut).not.toHaveBeenCalled();
  });

  describe('readToken', () => {
    it('reads token=… from the fragment', () => {
      expect(readToken('token=abc')).toBe('abc');
      expect(readToken('x=1&token=abc')).toBe('abc');
    });

    it('null when missing or empty', () => {
      expect(readToken(null)).toBeNull();
      expect(readToken('')).toBeNull();
      expect(readToken('token=')).toBeNull();
      expect(readToken('tok=abc')).toBeNull();
    });
  });
});
