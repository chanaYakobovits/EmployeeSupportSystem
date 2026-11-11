import { LoadChildrenCallback, Route } from '@angular/router';

import { DEV_AREAS } from '../../features/dev/dev-area';
import { roleGuard } from '../auth/guards/auth.guards';
import { Role } from '../models/auth.models';

/**
 * An area of the app: one menu item and one guarded route.
 * The single source of truth for "who may see what" in the client (spec 2.4) –
 * both the sidebar and the routes' roleGuard read the roles from here. The server enforces the same rules.
 */
export interface AppArea {
  id: string;
  /** Route path under the shell (no leading slash). */
  path: string;
  title: string;
  icon: string;
  /** The user needs at least one of these roles. */
  roles: readonly Role[];
}

/** Fixed order = menu order. Every user is an Employee (spec 2.3), so "my tickets" is open to all. */
export const APP_AREAS: readonly AppArea[] = [
  { id: 'employee', path: 'employee', title: 'הפניות שלי', icon: 'inbox', roles: ['Employee'] },
  { id: 'handler', path: 'handler', title: 'תור פניות', icon: 'support_agent', roles: ['Handler'] },
  { id: 'dashboard', path: 'dashboard', title: 'דשבורד', icon: 'monitoring', roles: ['DepartmentManager', 'Executive'] },
  { id: 'admin', path: 'admin', title: 'ניהול', icon: 'settings', roles: ['Admin'] },
];

/** Includes the development-only area ("בדיקות"); DEV_AREAS is empty in the production build. */
export const ALL_AREAS: readonly AppArea[] = [...APP_AREAS, ...DEV_AREAS];

/** The areas open to a user with these roles (union of all of them), in menu order. */
export function visibleAreas(roles: readonly Role[], areas: readonly AppArea[] = ALL_AREAS): AppArea[] {
  return areas.filter((area) => area.roles.some((role) => roles.includes(role)));
}

/** The guarded route of an area. data.title is bound to the page's title input. */
export function areaRoute(area: AppArea, loadChildren: LoadChildrenCallback): Route {
  return {
    path: area.path,
    canActivate: [roleGuard],
    data: { roles: area.roles, title: area.title },
    loadChildren,
  };
}
