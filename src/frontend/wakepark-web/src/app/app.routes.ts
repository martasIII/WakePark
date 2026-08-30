import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'parks' },
  {
    path: 'prihlaseni',
    title: 'Přihlášení · WakePark',
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: 'parks',
    title: 'Areály · WakePark',
    canActivate: [authGuard],
    loadComponent: () => import('./features/parks/park-list').then((m) => m.ParkList),
  },
  { path: '**', redirectTo: 'parks' },
];
