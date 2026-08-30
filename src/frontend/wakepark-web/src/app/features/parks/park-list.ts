import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed } from '@angular/core';

import { Park } from '../../core/api/tenancy.models';

/**
 * Přehled všech areálů.
 *
 * Data se načítají přes `httpResource`, takže stav načítání i chyba jsou signály
 * a v šabloně se nemusí ručně držet žádné `isLoading` proměnné.
 */
@Component({
  selector: 'wp-park-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './park-list.html',
  styleUrl: './park-list.scss',
})
export class ParkList {
  /** Cesta jde přes proxy na gateway, stejně jako v produkci. */
  protected readonly parks = httpResource<Park[]>(() => '/api/v1/tenancy/parks', {
    defaultValue: [],
  });

  protected readonly isEmpty = computed(
    () => !this.parks.isLoading() && this.parks.error() === undefined && this.parks.value().length === 0,
  );

  /** Politika storna jednou větou, ať se to v přehledu dá přečíst. */
  protected cancellationSummary(park: Park): string {
    const policy = park.cancellationPolicy;

    if (!policy.cancellationAllowed) {
      return 'Storno není možné';
    }

    const late =
      policy.lateCancellationRefundPercent > 0
        ? `, potom ${policy.lateCancellationRefundPercent} %`
        : ', potom bez náhrady';

    return `Zdarma do ${policy.freeCancellationHours} h před začátkem${late}`;
  }

  protected reload(): void {
    this.parks.reload();
  }
}
