import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Blokace (ADR 0009). Zatím kostra, obrazovka přijde v dalším kroku. */
@Component({
  selector: 'wp-blockings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Blokace</h1>
    <p>Rychlé zablokování vleku nebo areálu z mobilu — připravuje se.</p>
  `,
})
export class BlockingsPage {}
