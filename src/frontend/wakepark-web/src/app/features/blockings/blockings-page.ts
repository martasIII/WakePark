import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';

import type { BlockingDto, CableDetailDto, OperatingCalendarDto } from '../../core/api/api';
import { ParkContext } from '../../core/park/park-context';
import { blockingCategory } from '../../shared/blockings';
import { addDays, localClock, localDate, shortDate, shortTime } from '../../shared/park-time';
import { problemMessage } from '../../shared/problem';
import { BlockingDialog, BlockingDialogData } from './blocking-dialog';
import { BlockingsApi } from './blockings-api';

/** Kolik dní dopředu seznam ukazuje. */
const DAYS_AHEAD = 30;

interface BlockingRow {
  readonly id: string;
  readonly time: string;
  readonly where: string;
  readonly category: string;
  readonly note: string | null;
  readonly isActive: boolean;
}

interface BlockingDay {
  readonly date: string;
  readonly title: string;
  readonly rows: readonly BlockingRow[];
}

/**
 * Blokace (ADR 0009): co je zablokované dnes a v příštích dnech, založení a zrušení.
 * Hlavně pro obsluhu na mobilu — seznam pod sebou, velká tlačítka. Skončené blokace
 * se neukazují; v kalendáři zůstanou vidět.
 */
@Component({
  selector: 'wp-blockings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatProgressBarModule],
  templateUrl: './blockings-page.html',
  styleUrl: './blockings-page.scss',
})
export class BlockingsPage {
  private readonly park = inject(ParkContext);
  private readonly api = inject(BlockingsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  private readonly timeZone = computed(() => this.park.current()?.timeZoneId ?? 'Europe/Prague');

  /** Okamžik „teď“ — obnoví se po každé změně, ať zmizí, co mezitím skončilo. */
  private readonly now = signal(new Date());
  private readonly today = computed(() => localDate(this.now(), this.timeZone()));

  protected readonly calendar = httpResource<OperatingCalendarDto>(() => {
    const park = this.park.current();
    return park
      ? `/api/v1/operations/parks/${park.id}/calendar?from=${this.today()}&to=${addDays(this.today(), DAYS_AHEAD)}`
      : undefined;
  });

  protected readonly cables = httpResource<CableDetailDto[]>(
    () => {
      const park = this.park.current();
      return park ? `/api/v1/operations/parks/${park.id}/cables` : undefined;
    },
    { defaultValue: [] },
  );

  /** Řádek, u kterého se čeká na potvrzení zrušení. */
  protected readonly confirming = signal<string | null>(null);
  protected readonly removing = signal(false);

  protected readonly days = computed<BlockingDay[]>(() => {
    const now = this.now();
    const timeZone = this.timeZone();
    const today = this.today();
    const cableNames = new Map(this.cables.value().map((cable) => [cable.id, cable.name]));

    const groups = new Map<string, BlockingRow[]>();
    for (const blocking of [...(this.calendar.value()?.blockings ?? [])].sort((a, b) => a.startUtc.localeCompare(b.startUtc))) {
      if (new Date(blocking.endUtc) <= now) {
        continue;
      }

      // Blokace, která začala dřív a pořád trvá, patří k dnešku.
      const start = localDate(blocking.startUtc, timeZone);
      const date = start < today ? today : start;
      const rows = groups.get(date) ?? [];
      rows.push(toRow(blocking, cableNames, timeZone, now));
      groups.set(date, rows);
    }

    return [...groups].map(([date, rows]) => ({
      date,
      title: date === today ? `Dnes, ${shortDate(date)}` : date === addDays(today, 1) ? `Zítra, ${shortDate(date)}` : shortDate(date),
      rows,
    }));
  });

  protected block(): void {
    const park = this.park.current();
    if (!park) {
      return;
    }

    const data: BlockingDialogData = {
      parkId: park.id,
      timeZone: this.timeZone(),
      cables: this.cables
        .value()
        .filter((cable) => cable.isActive)
        .map((cable) => ({ id: cable.id, name: cable.name })),
      openingHours: new Map(
        (this.calendar.value()?.days ?? []).map((day) => [
          day.date,
          { opensAt: day.opensAt.slice(0, 5), closesAt: day.closesAt.slice(0, 5) },
        ]),
      ),
    };

    this.dialog
      .open(BlockingDialog, { data, width: '32rem', maxWidth: '100vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved?: boolean) => {
        if (saved) {
          this.refresh();
          this.snackBar.open('Zablokováno. Online rezervace v tom čase nepůjdou.', 'OK', { duration: 5000 });
        }
      });
  }

  protected async remove(row: BlockingRow): Promise<void> {
    const park = this.park.current();
    if (!park) {
      return;
    }

    this.removing.set(true);
    try {
      await this.api.remove(park.id, row.id);
      this.confirming.set(null);
      this.refresh();
      this.snackBar.open('Blokace zrušena.', 'OK', { duration: 4000 });
    } catch (error) {
      this.snackBar.open(problemMessage(error), 'OK', { duration: 8000 });
    } finally {
      this.removing.set(false);
    }
  }

  private refresh(): void {
    this.now.set(new Date());
    this.calendar.reload();
  }
}

function toRow(blocking: BlockingDto, cableNames: Map<string, string>, timeZone: string, now: Date): BlockingRow {
  const startDate = localDate(blocking.startUtc, timeZone);
  // Konec je exkluzivní: blokace do půlnoci končí „ve 24:00“ téhož dne, ne v 0:00 dalšího.
  const lastMoment = new Date(new Date(blocking.endUtc).getTime() - 1);
  const endDate = localDate(lastMoment, timeZone);
  const end = shortTime(localClock(blocking.endUtc, timeZone));
  const endText = endDate === startDate ? (end === '0:00' ? '24:00' : end) : `${shortDate(endDate)} ${end === '0:00' ? '24:00' : end}`;

  return {
    id: blocking.id,
    time: `${shortTime(localClock(blocking.startUtc, timeZone))}–${endText}`,
    where: blocking.cableId ? (cableNames.get(blocking.cableId) ?? 'Vlek') : 'Celý areál',
    category: blockingCategory(blocking.category).label,
    note: blocking.note ?? null,
    isActive: new Date(blocking.startUtc) <= now,
  };
}
