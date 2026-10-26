import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withFetch, withXsrfConfiguration } from '@angular/common/http';
import localeCs from '@angular/common/locales/cs';
import {
  ApplicationConfig,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';

// Backoffice je jen česky (ADR 0011): data, čísla a měna v českém formátu.
registerLocaleData(localeCs);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(
      // withFetch: moderní přenos místo XHR, kvůli němu funguje i rušení požadavků.
      withFetch(),
      // Angular přečte token z cookie a doplní ho do hlavičky sám. Názvy musí odpovídat
      // tomu, co vystavuje backend.
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    { provide: LOCALE_ID, useValue: 'cs' },
    { provide: MAT_DATE_LOCALE, useValue: 'cs-CZ' },
    provideNativeDateAdapter(),
    // Před prvním vykreslením zjistíme, jestli session existuje. Bez toho by guard
    // odmítl i přihlášeného uživatele, protože by o něm ještě nevěděl.
    provideAppInitializer(() => inject(AuthService).restore()),
  ],
};
