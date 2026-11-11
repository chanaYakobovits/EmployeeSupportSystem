import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { Role } from '../../models/auth.models';
import { AuthService } from '../auth.service';

/** Signed-in users only; others go to the login screen and come back afterwards (returnUrl). */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) {
    return true;
  }
  const queryParams = state.url && state.url !== '/' ? { returnUrl: state.url } : {};
  return inject(Router).createUrlTree(['/login'], { queryParams });
};

/** Spec 4.1: while a password change is pending, the rest of the app is closed. */
export const mustChangePasswordGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.mustChangePassword() ? inject(Router).createUrlTree(['/change-password']) : true;
};

/** The first-login change-password screen exists only while the flag is set. */
export const changePasswordGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.mustChangePassword() ? true : inject(Router).createUrlTree(['/']);
};

/** The login screen is for guests; a signed-in user is sent on. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (!auth.isAuthenticated()) {
    return true;
  }
  return inject(Router).createUrlTree([auth.mustChangePassword() ? '/change-password' : '/']);
};

/**
 * Requires one of route.data['roles']. No roles listed → allowed.
 * UX only – the server enforces permissions on every request.
 */
export const roleGuard: CanActivateFn = (route) => {
  const roles = (route.data['roles'] as readonly Role[] | undefined) ?? [];
  if (roles.length === 0 || inject(AuthService).hasAnyRole(roles)) {
    return true;
  }
  return inject(Router).createUrlTree(['/']);
};
