import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { MatIconRegistry } from '@angular/material/icon';

import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: authInterceptor (outer) handles 401 + refresh; errorInterceptor (inner) shows toasts.
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    // <mat-icon> uses the self-hosted Material Symbols font (see styles.scss).
    provideAppInitializer(() => {
      inject(MatIconRegistry).setDefaultFontSetClass('material-symbols-outlined');
    }),
    // Restore the session after a page reload: one refresh attempt from the HttpOnly cookie.
    // On failure the user is simply signed out – no error message.
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
};
