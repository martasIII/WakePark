import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';

import type { ParkDto } from '../api/api';
import { AuthService } from '../auth/auth.service';

const STORAGE_KEY = 'wp.parkId';

/**
 * Areál, se kterým backoffice právě pracuje. Wake Park má jeden, takže se vybere sám;
 * výběr v liště se ukáže, až jich bude víc (ADR 0011). Volbu si pamatuje prohlížeč.
 */
@Injectable({ providedIn: 'root' })
export class ParkContext {
  private readonly auth = inject(AuthService);

  /** Areály provozovatele z přihlášení. Načítají se jen pro přihlášeného uživatele. */
  readonly parks = httpResource<ParkDto[]>(
    () => (this.auth.isAuthenticated() ? '/api/v1/tenancy/parks' : undefined),
    { defaultValue: [] },
  );

  private readonly chosenId = signal<string | null>(readStored());

  /** Vybraný areál: zvolený, jinak první. Null, dokud se areály nenačtou nebo žádný není. */
  readonly current = computed<ParkDto | null>(() => {
    const parks = this.parks.value();
    return parks.find((park) => park.id === this.chosenId()) ?? parks[0] ?? null;
  });

  readonly hasChoice = computed(() => this.parks.value().length > 1);

  select(parkId: string): void {
    this.chosenId.set(parkId);
    try {
      localStorage.setItem(STORAGE_KEY, parkId);
    } catch {
      // Soukromé okno nebo zakázané úložiště: volba platí jen do zavření stránky.
    }
  }
}

function readStored(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}
