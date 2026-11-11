import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { fakeUser } from '../../core/auth/testing/auth-fixtures';
import { CurrentUser } from '../../core/models/auth.models';
import { Header } from './header';

describe('Header', () => {
  const currentUser = signal<CurrentUser | null>(null);
  const logout = jasmine.createSpy('logout').and.resolveTo(undefined);
  let fixture: ComponentFixture<Header>;
  let element: HTMLElement;

  beforeEach(() => {
    logout.calls.reset();
    TestBed.configureTestingModule({
      imports: [Header],
      providers: [provideRouter([]), { provide: AuthService, useValue: { currentUser, logout } }],
    });
    fixture = TestBed.createComponent(Header);
    element = fixture.nativeElement;
  });

  afterEach(() => document.querySelectorAll('.cdk-overlay-container').forEach((el) => (el.innerHTML = '')));

  function render(user: CurrentUser, compact = false): void {
    currentUser.set(user);
    fixture.componentRef.setInput('compact', compact);
    fixture.detectChanges();
  }

  it('shows the logo, initials, full name and job title', () => {
    render(fakeUser({ firstName: 'יעל', lastName: 'הרפז', jobTitle: 'מנהלת משאבי אנוש' }));

    expect(element.querySelector('.brand-name')?.textContent).toBe('עובדנט');
    expect(element.querySelector('.brand-slogan')?.textContent).toBe('הופכים פניות לפתרונות');
    expect(element.querySelector('.avatar')?.textContent?.trim()).toBe('יה');
    expect(element.querySelector('.user-name')?.textContent).toBe('יעל הרפז');
    expect(element.querySelector('.user-role')?.textContent).toBe('מנהלת משאבי אנוש');
  });

  it('without a job title shows the most senior role in Hebrew', () => {
    render(fakeUser({ jobTitle: '', roles: ['Employee', 'Handler', 'Admin'] }));

    expect(element.querySelector('.user-role')?.textContent).toBe('מנהל/ת מערכת');
  });

  it('compact (mobile): only the menu button and the avatar', () => {
    render(fakeUser(), true);

    expect(element.querySelector('.menu-button')).not.toBeNull();
    expect(element.querySelector('.avatar')).not.toBeNull();
    expect(element.querySelector('.brand')).toBeNull();
    expect(element.querySelector('.user-info')).toBeNull();
  });

  it('has no notifications bell', () => {
    render(fakeUser());
    expect(element.textContent).not.toContain('notifications');
  });

  it('the user menu offers "change password" (with returnUrl) and "sign out"', async () => {
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);
    spyOnProperty(router, 'url').and.returnValue('/handler');
    render(fakeUser());

    (element.querySelector('.user-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();

    const items = Array.from(document.querySelectorAll('.user-menu-panel [mat-menu-item]')) as HTMLButtonElement[];
    expect(items.map((item) => item.textContent!.trim())).toEqual([
      jasmine.stringContaining('החלפת סיסמה'),
      jasmine.stringContaining('יציאה'),
    ]);

    items[0].click();
    expect(navigate).toHaveBeenCalledWith(['/account/change-password'], { queryParams: { returnUrl: '/handler' } });
  });

  it('"sign out" calls AuthService.logout', async () => {
    render(fakeUser());
    (element.querySelector('.user-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();

    const items = Array.from(document.querySelectorAll('.user-menu-panel [mat-menu-item]')) as HTMLButtonElement[];
    items[1].click();

    expect(logout).toHaveBeenCalled();
  });
});
