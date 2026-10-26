import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed } from '@angular/core';

import { ParkDto, num } from '../../core/api/api';

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
  /** Areály provozovatele z přihlášení (ADR 0007). */
  protected readonly parks = httpResource<ParkDto[]>(() => '/api/v1/tenancy/parks', {
    defaultValue: [],
  });

  protected readonly isEmpty = computed(
    () => !this.parks.isLoading() && this.parks.error() === undefined && this.parks.value().length === 0,
  );

  /** Politika storna jednou větou, ať se to v přehledu dá přečíst. */
  protected cancellationSummary(park: ParkDto): string {
    const policy = park.cancellationPolicy;

    if (!policy.cancellationAllowed) {
      return 'Storno není možné';
    }

    const latePercent = num(policy.lateCancellationRefundPercent);
    const late =
      latePercent > 0
        ? `, potom ${latePercent} %`
        : ', potom bez náhrady';

    return `Zdarma do ${num(policy.freeCancellationHours)} h před začátkem${late}`;
  }

  protected reload(): void {
    this.parks.reload();
  }
}
