import { inject } from '@angular/core';
import { Router, Routes, UrlTree } from '@angular/router';

import { authGuard, permissionGuard } from './core/auth/auth.guard';
import { Permissions } from './core/auth/auth.models';
import { AuthService } from './core/auth/auth.service';

/**
 * Úvodní obrazovka podle toho, co uživatel smí (ADR 0008): admin a vedoucí kalendář,
 * obsluha blokace. Bez oprávnění vysvětlení místo smyčky přesměrování.
 */
function home(): string | UrlTree {
  const auth = inject(AuthService);

  // Přesměrování běží dřív než guardy: nepřihlášený rovnou na přihlášení, jinak by se
  // „úvodní obrazovka“ vyhodnotila bez oprávnění a po přihlášení by skončil na „bez oprávnění“.
  if (!auth.isAuthenticated()) {
    return inject(Router).createUrlTree(['/prihlaseni']);
  }

  if (auth.has(Permissions.operationsRead)) {
    return 'kalendar';
  }

  if (auth.has(Permissions.blockingsManage)) {
    return 'blokace';
  }

  return auth.has(Permissions.catalogManage) ? 'katalog' : 'bez-opravneni';
}

export const routes: Routes = [
  {
    path: 'prihlaseni',
    title: 'Přihlášení · WakePark',
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: home },
      {
        path: 'kalendar',
        title: 'Provozní kalendář · WakePark',
        canActivate: [permissionGuard(Permissions.operationsRead)],
        loadComponent: () => import('./features/calendar/calendar-page').then((m) => m.CalendarPage),
      },
      {
        path: 'blokace',
        title: 'Blokace · WakePark',
        canActivate: [permissionGuard(Permissions.blockingsManage)],
        loadComponent: () => import('./features/blockings/blockings-page').then((m) => m.BlockingsPage),
      },
      {
        path: 'katalog',
        title: 'Katalog a ceny · WakePark',
        canActivate: [permissionGuard(Permissions.catalogManage)],
        loadComponent: () => import('./features/catalog/catalog-page').then((m) => m.CatalogPage),
      },
      {
        path: 'areal',
        title: 'Areál · WakePark',
        canActivate: [permissionGuard(Permissions.parksRead)],
        loadComponent: () => import('./features/parks/park-list').then((m) => m.ParkList),
      },
      {
        path: 'bez-opravneni',
        title: 'Bez oprávnění · WakePark',
        loadComponent: () => import('./features/no-access').then((m) => m.NoAccess),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
