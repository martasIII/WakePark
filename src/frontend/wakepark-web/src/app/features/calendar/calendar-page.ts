import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';

import type { BlockingDto, CableDetailDto, OperatingCalendarDto, OperatingDaysImportDto } from '../../core/api/api';
import { num } from '../../core/api/api';
import { Permissions } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { ParkContext } from '../../core/park/park-context';
import { daysOfMonth, localDate, localTime, shortTime, weekdayIndex, weekdayName } from '../../shared/park-time';
import { DayDialog, DayDialogData } from './day-dialog';
import { ImportDialog, ImportDialogData } from './import-dialog';

/** Den v kalendáři připravený k vykreslení. */
interface CalendarDay {
  readonly date: string;
  readonly dayOfMonth: number;
  readonly weekday: string;
  readonly isWeekend: boolean;
  readonly isToday: boolean;
  readonly hours: string | null;
  readonly opensAt: string | null;
  readonly closesAt: string | null;
  readonly blockings: readonly string[];
  /** Celý popis pro čtečku obrazovky: „sobota 31. 10., 10:00–19:00“. */
  readonly label: string;
}

const BLOCKING_LABELS: Record<string, string> = {
  TechnicalFault: 'závada',
  PrivateEvent: 'akce',
  Weather: 'počasí',
  Other: 'mimo provoz',
};

const MONTHS = [
  'leden', 'únor', 'březen', 'duben', 'květen', 'červen',
  'červenec', 'srpen', 'září', 'říjen', 'listopad', 'prosinec',
];

/**
 * Provozní kalendář (ADR 0009): měsíc po dnech, den bez záznamu = zavřeno. Úprava dne a import
 * CSV jen s oprávněním `operations.manage`; ostatní kalendář jen vidí. Na mobilu seznam dní.
 */
@Component({
  selector: 'wp-calendar-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatProgressBarModule],
  templateUrl: './calendar-page.html',
  styleUrl: './calendar-page.scss',
})
export class CalendarPage {
  private readonly auth = inject(AuthService);
  private readonly park = inject(ParkContext);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly canEdit = computed(() => this.auth.has(Permissions.operationsManage, this.park.current()?.id));

  private readonly today = new Date();
  protected readonly year = signal(this.today.getFullYear());
  protected readonly month = signal(this.today.getMonth() + 1);
  protected readonly monthTitle = computed(() => `${MONTHS[this.month() - 1]} ${this.year()}`);
  protected readonly weekdays = ['po', 'út', 'st', 'čt', 'pá', 'so', 'ne'];

  private readonly days = computed(() => daysOfMonth(this.year(), this.month()));

  protected readonly calendar = httpResource<OperatingCalendarDto>(() => {
    const park = this.park.current();
    const days = this.days();
    return park ? `/api/v1/operations/parks/${park.id}/calendar?from=${days[0]}&to=${days[days.length - 1]}` : undefined;
  });

  protected readonly cables = httpResource<CableDetailDto[]>(
    () => {
      const park = this.park.current();
      return park ? `/api/v1/operations/parks/${park.id}/cables` : undefined;
    },
    { defaultValue: [] },
  );

  /** Prázdné buňky před prvním dnem, ať pondělí sedí pod pondělím. */
  protected readonly leadingBlanks = computed(() => Array.from({ length: weekdayIndex(this.days()[0]) }));

  protected readonly calendarDays = computed<CalendarDay[]>(() => {
    const data = this.calendar.value();
    const timeZone = data?.timeZoneId ?? 'Europe/Prague';
    const todayKey = localDate(new Date(), timeZone);
    const byDate = new Map((data?.days ?? []).map((day) => [day.date, day]));
    const cableNames = new Map(this.cables.value().map((cable) => [cable.id, cable.name]));

    return this.days().map((date) => {
      const day = byDate.get(date);
      const hours = day ? `${shortTime(day.opensAt)}–${shortTime(day.closesAt)}` : null;
      const blockings = (data?.blockings ?? [])
        .filter((blocking) => touches(blocking, date, timeZone))
        .map((blocking) => describe(blocking, cableNames, timeZone));

      return {
        date,
        dayOfMonth: Number(date.slice(8)),
        weekday: weekdayName(date),
        isWeekend: weekdayIndex(date) >= 5,
        isToday: date === todayKey,
        hours,
        opensAt: day ? shortTime(day.opensAt) : null,
        closesAt: day ? shortTime(day.closesAt) : null,
        blockings,
        label: [`${weekdayName(date)} ${Number(date.slice(8))}. ${Number(date.slice(5, 7))}.`, hours ?? 'zavřeno', ...blockings].join(', '),
      };
    });
  });

  protected readonly openDaysText = computed(() => {
    const count = this.calendarDays().filter((day) => day.hours).length;
    return `${count} ${count === 1 ? 'den' : count >= 2 && count <= 4 ? 'dny' : 'dní'}`;
  });

  protected previousMonth(): void {
    this.shiftMonth(-1);
  }

  protected nextMonth(): void {
    this.shiftMonth(1);
  }

  protected editDay(day: CalendarDay): void {
    const park = this.park.current();
    if (!park || !this.canEdit()) {
      return;
    }

    const data: DayDialogData = { parkId: park.id, date: day.date, opensAt: day.opensAt, closesAt: day.closesAt };
    this.dialog
      .open(DayDialog, { data, width: '24rem', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((saved?: boolean) => {
        if (saved) {
          this.calendar.reload();
        }
      });
  }

  protected importCsv(): void {
    const park = this.park.current();
    if (!park) {
      return;
    }

    const data: ImportDialogData = { parkId: park.id };
    this.dialog
      .open(ImportDialog, { data, width: '44rem', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((result?: OperatingDaysImportDto) => {
        if (result) {
          this.calendar.reload();
          this.snackBar.open(
            `Uloženo: ${num(result.added)} nových, ${num(result.changed)} změněných, ${num(result.closed)} zavřených dní.`,
            'OK',
            { duration: 6000 },
          );
        }
      });
  }

  private shiftMonth(delta: number): void {
    const index = this.year() * 12 + (this.month() - 1) + delta;
    this.year.set(Math.floor(index / 12));
    this.month.set((index % 12) + 1);
  }
}

/** Zasahuje blokace do daného dne (v čase areálu)? Konec je exkluzivní. */
function touches(blocking: BlockingDto, date: string, timeZone: string): boolean {
  const start = localDate(blocking.startUtc, timeZone);
  const end = localDate(new Date(new Date(blocking.endUtc).getTime() - 1), timeZone);
  return start <= date && date <= end;
}

function describe(blocking: BlockingDto, cableNames: Map<string, string>, timeZone: string): string {
  const where = blocking.cableId ? (cableNames.get(blocking.cableId) ?? 'vlek') : 'celý areál';
  const why = BLOCKING_LABELS[blocking.category] ?? blocking.category;
  return `${localTime(blocking.startUtc, timeZone)}–${localTime(blocking.endUtc, timeZone)} ${where}: ${why}`;
}
