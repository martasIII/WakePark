import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { CurrentUser } from './auth.models';

/** Přihlášení se nezdařilo kvůli špatným údajům, ne kvůli výpadku. */
export class InvalidCredentialsError extends Error {
  constructor() {
    super('Nesprávný e-mail nebo heslo.');
    this.name = 'InvalidCredentialsError';
  }
}

/**
 * Stav přihlášení.
 *
 * Žádný token si tu nedržíme — session je v `HttpOnly` cookie, ke které se JavaScript
 * nedostane. Prohlížeč ji přikládá sám, takže tu není ani interceptor, který by ji doplňoval.
 * Ochranný token proti CSRF řeší Angular zabudovaně.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly currentUser = signal<CurrentUser | null>(null);

  /** Přihlášený uživatel, nebo null. */
  readonly user = this.currentUser.asReadonly();

  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  /**
   * Zjistí, jestli už session existuje. Volá se při startu aplikace — kromě obnovení stavu
   * tím vznikne i cookie s ochranným tokenem, kterou pak potřebuje přihlašovací formulář.
   */
  async restore(): Promise<void> {
    try {
      this.currentUser.set(await firstValueFrom(this.http.get<CurrentUser>('/api/v1/identity/me')));
    } catch {
      // 401 je běžný stav, ne chyba: uživatel prostě není přihlášený.
      this.currentUser.set(null);
    }
  }

  async signIn(email: string, password: string): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/v1/identity/login', { email, password }));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        throw new InvalidCredentialsError();
      }

      throw error;
    }

    await this.restore();
  }

  async signOut(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/v1/identity/logout', {}));
    } finally {
      this.currentUser.set(null);
    }
  }

  /**
   * Má přihlášený uživatel dané oprávnění?
   *
   * Slouží jen k tomu, aby se neukazovala tlačítka, která by stejně neprošla. Skutečnou
   * kontrolu dělá backend — na frontend se v otázkách oprávnění spoléhat nelze.
   */
  has(permission: string, wakeParkId?: string): boolean {
    const user = this.currentUser();

    if (!user) {
      return false;
    }

    if (user.globalPermissions.includes(permission)) {
      return true;
    }

    const parks = wakeParkId ? user.parks.filter((park) => park.wakeParkId === wakeParkId) : user.parks;

    return parks.some((park) => park.permissions.includes(permission));
  }
}
