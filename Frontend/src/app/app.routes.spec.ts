import { Route } from '@angular/router';

import { roleGuard } from './core/auth/guards/auth.guards';
import { ALL_AREAS } from './core/navigation/app-areas';
import { routes } from './app.routes';

describe('app routes', () => {
  const shellChildren: Route[] = routes.find((route) => route.path === '' && route.children)?.children ?? [];

  it('has the shell with child routes', () => {
    expect(shellChildren.length).toBeGreaterThan(0);
  });

  for (const area of ALL_AREAS) {
    it(`guards "${area.path}" with the roles of the same area definition as the menu`, () => {
      const route = shellChildren.find((child) => child.path === area.path);

      expect(route).withContext(`route for area ${area.id}`).toBeDefined();
      expect(route!.canActivate).toContain(roleGuard);
      // The very same array – not a copy that could drift.
      expect(route!.data?.['roles']).toBe(area.roles);
      expect(route!.data?.['title']).toBe(area.title);
    });
  }

  it('every role-guarded route is an area (no roles defined anywhere else)', () => {
    const areaPaths = ALL_AREAS.map((area) => area.path);
    const guardedPaths = shellChildren
      .filter((child) => child.canActivate?.includes(roleGuard))
      .map((child) => child.path);

    expect(guardedPaths).toEqual(areaPaths);
  });

  it('the home page of a signed-in user is "my tickets"', () => {
    const home = shellChildren.find((child) => child.path === '' && child.pathMatch === 'full');
    expect(home?.redirectTo).toBe('employee');
  });

  it('voluntary password change is inside the shell, the mandatory one outside it', () => {
    expect(shellChildren.find((child) => child.path === 'account/change-password')?.data?.['mode']).toBe('voluntary');
    expect(routes.find((route) => route.path === 'change-password')?.data?.['mode']).toBeUndefined();
  });
});
