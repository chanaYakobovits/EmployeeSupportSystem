import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideLocationMocks } from '@angular/common/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { AuthService } from '../../../core/auth/auth.service';
import { loginNoticeState } from '../login-notices';
import { LoginPage } from './login-page';

@Component({ template: '' })
class Start {}

describe('LoginPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: LoginPage },
          { path: 'start', component: Start },
        ]),
        provideLocationMocks(),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: AuthService, useValue: jasmine.createSpyObj<AuthService>('AuthService', ['login']) },
      ],
    });
  });

  async function openLogin(state?: Record<string, string>): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create('/start');
    await TestBed.inject(Router).navigate(['/login'], { state });
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  it('shows the notice left by set-password, as a status message above the form', async () => {
    const element = await openLogin(loginNoticeState('passwordSet'));

    const notice = element.querySelector('.auth-alert-success')!;
    expect(notice.getAttribute('role')).toBe('status');
    expect(notice.textContent).toContain('הסיסמה נקבעה. אפשר להתחבר עם המייל או קוד העובד.');
  });

  it('no notice without one', async () => {
    expect((await openLogin()).querySelector('.auth-alert-success')).toBeNull();
  });

  it('no notice for an unknown key (only known keys travel; the text is fixed in the code)', async () => {
    expect((await openLogin({ loginNotice: '<b>hi</b>' })).querySelector('.auth-alert-success')).toBeNull();
  });

  it('keeps the split card with the brand side', async () => {
    const element = await openLogin();
    expect(element.querySelector('.auth-split-card app-auth-brand')).not.toBeNull();
    expect(element.textContent).toContain('הופכים פניות לפתרונות');
  });
});
