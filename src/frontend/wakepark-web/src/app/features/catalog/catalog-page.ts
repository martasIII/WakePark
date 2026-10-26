import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Katalog a ceny (ADR 0009). Zatím kostra, obrazovka přijde v dalším kroku. */
@Component({
  selector: 'wp-catalog-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Katalog a ceny</h1>
    <p>Kategorie, produkty a ceny pro web, e-shop a pokladnu — připravuje se.</p>
  `,
})
export class CatalogPage {}
