import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'parks' },
  {
    path: 'parks',
    title: 'Areály · WakePark',
    loadComponent: () => import('./features/parks/park-list').then((m) => m.ParkList),
  },
  { path: '**', redirectTo: 'parks' },
];
