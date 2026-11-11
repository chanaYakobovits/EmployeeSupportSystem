import type { AppArea } from '../../core/navigation/app-areas';

// TEMPORARY – development-only area. In the production build this file is replaced by
// dev-area.production.ts (angular.json fileReplacements), so none of the dev code is bundled.
// Its route is built in app.routes.ts from this definition, like every other area.

export const DEV_AREAS: readonly AppArea[] = [
  { id: 'dev', path: 'dev', title: 'בדיקות (פיתוח)', icon: 'bug_report', roles: ['Employee'] },
];

export const loadDevRoutes = () => import('./dev.routes').then((m) => m.DEV_ROUTES);
