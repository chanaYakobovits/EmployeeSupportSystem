import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { fakeUser } from '../../../core/auth/testing/auth-fixtures';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ChangePasswordMode, ChangePasswordPage } from './change-password-page';

describe('ChangePasswordPage', () => {
  let fixture: ComponentFixture<ChangePasswordPage>;
  let element: HTMLElement;
  let navigateByUrl: jasmine.Spy;
  let auth: { currentUser: ReturnType<typeof signal>; changePassword: jasmine.Spy; logout: jasmine.Spy };
  let notifications: jasmine.SpyObj<NotificationService>;

  beforeEach(() => {
    auth = {
      currentUser: signal(fakeUser()),
      changePassword: jasmine.createSpy('changePassword').and.returnValue(of(fakeUser())),
      logout: jasmine.createSpy('logout').and.resolveTo(undefined),
    };
    notifications = jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']);
    TestBed.configureTestingModule({
      imports: [ChangePasswordPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: NotificationService, useValue: notifications },
      ],
    });
    navigateByUrl = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
  });

  function render(mode?: ChangePasswordMode, returnUrl?: string): void {
    fixture = TestBed.createComponent(ChangePasswordPage);
    if (mode) {
      fixture.componentRef.setInput('mode', mode);
    }
    if (returnUrl) {
      fixture.componentRef.setInput('returnUrl', returnUrl);
    }
    fixture.detectChanges();
    element = fixture.nativeElement;
  }

  function type(control: string, value: string): void {
    const input = element.querySelector(`input[formcontrolname="${control}"]`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function submitValidForm(): void {
    type('currentPassword', 'Temp1234');
    type('newPassword', 'NewPass123');
    type('confirmPassword', 'NewPass123');
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  function buttonTexts(): string[] {
    return Array.from(element.querySelectorAll('button')).map((b) => b.textContent!.trim());
  }

  describe('required (first login)', () => {
    beforeEach(() => render());

    it('is a standalone auth card: no page header and no cancel', () => {
      expect(element.querySelector('.auth-page')).not.toBeNull();
      expect(element.querySelector('app-page-header')).toBeNull();
      expect(element.querySelector('.cancel-button')).toBeNull();
      expect(buttonTexts()).not.toContain('ביטול');
    });

    it('offers sign-out (not skip)', () => {
      expect(buttonTexts()).toContain('יציאה מהמערכת');
    });

    it('after success goes home', () => {
      submitValidForm();

      expect(auth.changePassword).toHaveBeenCalledWith({ currentPassword: 'Temp1234', newPassword: 'NewPass123' });
      expect(notifications.success).toHaveBeenCalled();
      expect(navigateByUrl).toHaveBeenCalledWith('/');
    });
  });

  describe('voluntary (user menu)', () => {
    beforeEach(() => render('voluntary', '/handler?page=2'));

    it('renders inside the page layout with a cancel button and without sign-out', () => {
      expect(element.querySelector('app-page-header')).not.toBeNull();
      expect(element.querySelector('.auth-page')).toBeNull();
      expect(element.querySelector('.cancel-button')).not.toBeNull();
      expect(buttonTexts()).not.toContain('יציאה מהמערכת');
    });

    it('cancel returns to the previous screen without calling the server', () => {
      (element.querySelector('.cancel-button') as HTMLButtonElement).click();

      expect(auth.changePassword).not.toHaveBeenCalled();
      expect(navigateByUrl).toHaveBeenCalledWith('/handler?page=2');
    });

    it('after success shows a message and returns to the previous screen', () => {
      submitValidForm();

      expect(notifications.success).toHaveBeenCalledWith('הסיסמה הוחלפה בהצלחה');
      expect(navigateByUrl).toHaveBeenCalledWith('/handler?page=2');
    });

    it('never returns to an external or auth URL', () => {
      fixture.componentRef.setInput('returnUrl', '//evil.example.test');
      fixture.detectChanges();
      (element.querySelector('.cancel-button') as HTMLButtonElement).click();

      expect(navigateByUrl).toHaveBeenCalledWith('/');
    });
  });
});
