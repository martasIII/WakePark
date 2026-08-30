import { provideHttpClient, withFetch, withXsrfConfiguration } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      // withFetch: moderní přenos místo XHR, kvůli němu funguje i rušení požadavků.
      withFetch(),
      // Angular přečte token z cookie a doplní ho do hlavičky sám. Názvy musí odpovídat
      // tomu, co vystavuje backend.
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    // Před prvním vykreslením zjistíme, jestli session existuje. Bez toho by guard
    // odmítl i přihlášeného uživatele, protože by o něm ještě nevěděl.
    provideAppInitializer(() => inject(AuthService).restore()),
  ],
};
