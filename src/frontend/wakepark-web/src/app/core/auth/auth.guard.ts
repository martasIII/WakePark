import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { Permission } from './auth.models';
import { AuthService } from './auth.service';

/**
 * Pustí dál jen přihlášeného uživatele, jinak přesměruje na přihlášení
 * a zapamatuje si, kam se chtěl dostat.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/prihlaseni'], { queryParams: { pokracovat: state.url } });
};

/**
 * Pustí dál jen s oprávněním — aby se obsluha neproklikala na obrazovku, která by jí stejně
 * vracela 403. Skutečnou kontrolu dělá API.
 */
export function permissionGuard(permission: Permission): CanActivateFn {
  return () => inject(AuthService).has(permission) || inject(Router).createUrlTree(['/']);
}
