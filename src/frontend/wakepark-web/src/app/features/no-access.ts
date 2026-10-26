import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Přihlášený uživatel bez oprávnění k čemukoli — vysvětlení místo prázdné stránky. */
@Component({
  selector: 'wp-no-access',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Zatím nemáte přidělené oprávnění</h1>
    <p>Účet funguje, ale ještě vám nikdo nepřidělil roli. Ozvěte se správci areálu.</p>
  `,
})
export class NoAccess {}
