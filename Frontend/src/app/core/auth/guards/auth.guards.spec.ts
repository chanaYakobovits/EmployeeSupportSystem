import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';

import { Role } from '../../models/auth.models';
import { AuthService } from '../auth.service';
import { authGuard, changePasswordGuard, guestGuard, mustChangePasswordGuard, roleGuard } from './auth.guards';

describe('auth guards', () => {
  const isAuthenticated = signal(false);
  const mustChangePassword = signal(false);
  let roles: Role[] = [];

  const fakeAuth = {
    isAuthenticated,
    mustChangePassword,
    hasAnyRole: (required: readonly Role[]) => required.some((r) => roles.includes(r)),
  };

  beforeEach(() => {
    isAuthenticated.set(false);
    mustChangePassword.set(false);
    roles = [];
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: fakeAuth }],
    });
  });

  function run(guard: CanActivateFn, url = '/', data: Record<string, unknown> = {}): true | string {
    const route = { data } as unknown as ActivatedRouteSnapshot;
    const state = { url } as RouterStateSnapshot;
    const result = TestBed.runInInjectionContext(() => guard(route, state));
    if (result === true) {
      return true;
    }
    expect(result).toBeInstanceOf(UrlTree);
    return TestBed.inject(Router).serializeUrl(result as UrlTree);
  }

  describe('authGuard', () => {
    it('allows a signed-in user', () => {
      isAuthenticated.set(true);
      expect(run(authGuard, '/employee')).toBeTrue();
    });

    it('sends a guest to login with returnUrl', () => {
      expect(run(authGuard, '/handler?page=2')).toBe('/login?returnUrl=%2Fhandler%3Fpage%3D2');
    });

    it('omits returnUrl for the home page', () => {
      expect(run(authGuard, '/')).toBe('/login');
    });
  });

  describe('mustChangePasswordGuard', () => {
    it('sends the user to the change-password screen while the flag is set', () => {
      isAuthenticated.set(true);
      mustChangePassword.set(true);
      expect(run(mustChangePasswordGuard)).toBe('/change-password');
    });

    it('allows when no change is pending', () => {
      isAuthenticated.set(true);
      expect(run(mustChangePasswordGuard)).toBeTrue();
    });
  });

  describe('changePasswordGuard', () => {
    it('opens the screen only while the flag is set', () => {
      mustChangePassword.set(true);
      expect(run(changePasswordGuard)).toBeTrue();
    });

    it('redirects home otherwise', () => {
      expect(run(changePasswordGuard)).toBe('/');
    });
  });

  describe('guestGuard', () => {
    it('allows a guest to the login screen', () => {
      expect(run(guestGuard, '/login')).toBeTrue();
    });

    it('sends a signed-in user home', () => {
      isAuthenticated.set(true);
      expect(run(guestGuard, '/login')).toBe('/');
    });

    it('sends a signed-in user with a pending change to the change-password screen', () => {
      isAuthenticated.set(true);
      mustChangePassword.set(true);
      expect(run(guestGuard, '/login')).toBe('/change-password');
    });
  });

  describe('roleGuard', () => {
    it('allows when the route lists no roles', () => {
      expect(run(roleGuard, '/employee')).toBeTrue();
    });

    it('allows when the user has one of the roles', () => {
      roles = ['Employee', 'Executive'];
      expect(run(roleGuard, '/dashboard', { roles: ['DepartmentManager', 'Executive'] })).toBeTrue();
    });

    it('redirects home when the user has none of the roles', () => {
      roles = ['Employee'];
      expect(run(roleGuard, '/admin', { roles: ['Admin'] })).toBe('/');
    });
  });
});
