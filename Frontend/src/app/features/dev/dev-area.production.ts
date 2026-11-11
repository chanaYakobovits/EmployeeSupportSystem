import { Routes } from '@angular/router';

import type { AppArea } from '../../core/navigation/app-areas';

// Production replacement for dev-area.ts: no dev area, no dev routes.
export const DEV_AREAS: readonly AppArea[] = [];

export const loadDevRoutes = (): Promise<Routes> => Promise.resolve([]);
