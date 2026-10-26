import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Provozní kalendář (ADR 0009). Zatím kostra, obrazovka přijde v dalším kroku. */
@Component({
  selector: 'wp-calendar-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Provozní kalendář</h1>
    <p>Otevírací doba po dnech, import CSV a blokace — připravuje se.</p>
  `,
})
export class CalendarPage {}
