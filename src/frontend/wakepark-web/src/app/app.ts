import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatSelectModule } from '@angular/material/select';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';

import { Permission, Permissions } from './core/auth/auth.models';
import { AuthService } from './core/auth/auth.service';
import { ParkContext } from './core/park/park-context';

interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly permission: Permission;
}

/** Položky menu. Každý vidí jen ty, na které má oprávnění (ADR 0008, 0011). */
const NAV: readonly NavItem[] = [
  { path: 'kalendar', label: 'Provozní kalendář', permission: Permissions.operationsRead },
  { path: 'blokace', label: 'Blokace', permission: Permissions.blockingsManage },
  { path: 'katalog', label: 'Katalog a ceny', permission: Permissions.catalogManage },
  { path: 'areal', label: 'Areál', permission: Permissions.parksRead },
];

/**
 * Rozložení backoffice: na desktopu menu vlevo napevno, na mobilu (obsluha u vody) schované
 * za tlačítkem, aby obsah dostal celou šířku.
 */
@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatButtonModule,
    MatSelectModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly park = inject(ParkContext);
  protected readonly user = this.auth.user;

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe('(max-width: 959.98px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly navItems = computed(() => (this.user() ? NAV.filter((item) => this.auth.has(item.permission)) : []));

  protected async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/prihlaseni');
  }
}
