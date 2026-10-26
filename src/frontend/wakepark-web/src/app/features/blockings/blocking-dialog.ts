import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { BLOCKING_CATEGORIES } from '../../shared/blockings';
import { addDays, localClock, localDate } from '../../shared/park-time';
import { problemMessage } from '../../shared/problem';
import { BlockingsApi } from './blockings-api';

export interface BlockingDialogData {
  readonly parkId: string;
  readonly timeZone: string;
  /** Aktivní vleky areálu. */
  readonly cables: readonly { readonly id: string; readonly name: string }[];
  /** Otevírací doba podle data (`HH:mm`); den, který tu není, je zavřený. */
  readonly openingHours: ReadonlyMap<string, { readonly opensAt: string; readonly closesAt: string }>;
}

/** Hodnota výběru „kde“ pro celý areál — API ho pozná podle chybějícího vleku. */
const WHOLE_PARK = 'park';
const NOTE_MAX_LENGTH = 500;

/**
 * Nová blokace (ADR 0009). Stavěné na mobil u vleku: výběr klepnutím, rychlé volby času
 * od teď, poznámka jen pro obsluhu. Časy jsou v čase areálu. Zavírá se `true`, když se uložilo.
 */
@Component({
  selector: 'wp-blocking-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatDialogModule, MatButtonModule, MatChipsModule, MatFormFieldModule, MatInputModule],
  templateUrl: './blocking-dialog.html',
  styleUrl: './blocking-dialog.scss',
})
export class BlockingDialog {
  private readonly api = inject(BlockingsApi);
  private readonly ref = inject<MatDialogRef<BlockingDialog, boolean>>(MatDialogRef);
  protected readonly data = inject<BlockingDialogData>(MAT_DIALOG_DATA);

  protected readonly categories = BLOCKING_CATEGORIES;
  protected readonly wholePark = WHOLE_PARK;
  protected readonly noteMaxLength = NOTE_MAX_LENGTH;

  private readonly today = localDate(new Date(), this.data.timeZone);

  protected readonly where = signal(this.data.cables.length === 1 ? this.data.cables[0].id : WHOLE_PARK);
  protected readonly category = signal<string | null>(null);
  protected readonly startDate = signal(this.today);
  protected readonly startTime = signal(roundDown(localClock(new Date(), this.data.timeZone)));
  protected readonly endDate = signal(this.today);
  protected readonly endTime = signal('');
  protected readonly note = signal('');
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Dnešní zavírací doba, jen když se ještě jezdí — jinak volba „do zavření“ nedává smysl. */
  protected readonly closingToday = computed(() => {
    const hours = this.data.openingHours.get(this.today);
    const now = localClock(new Date(), this.data.timeZone);
    return hours && hours.closesAt > now ? hours.closesAt : null;
  });

  protected readonly rangeValid = computed(
    () => !!this.endTime() && `${this.endDate()}T${this.endTime()}` > `${this.startDate()}T${this.startTime()}`,
  );

  protected readonly canSave = computed(() => !!this.category() && this.rangeValid() && !this.saving());

  protected setStartDate(date: string): void {
    this.startDate.set(date);
    if (date && this.endDate() < date) {
      this.endDate.set(date);
    }
  }

  /** Od teď na daný počet minut. */
  protected fromNow(minutes: number): void {
    const now = localClock(new Date(), this.data.timeZone);
    this.startDate.set(this.today);
    this.startTime.set(roundDown(now));

    const end = shift(this.today, roundDown(now), minutes);
    this.endDate.set(end.date);
    this.endTime.set(end.time);
  }

  protected untilClosing(): void {
    const closing = this.closingToday();
    if (!closing) {
      return;
    }

    this.startDate.set(this.today);
    this.startTime.set(roundDown(localClock(new Date(), this.data.timeZone)));
    this.endDate.set(this.today);
    this.endTime.set(closing);
  }

  /** Celý zvolený den: otevírací doba, nebo celých 24 hodin, když je ten den zavřeno. */
  protected wholeDay(): void {
    const date = this.startDate() || this.today;
    const hours = this.data.openingHours.get(date);

    this.startDate.set(date);
    this.startTime.set(hours?.opensAt ?? '00:00');
    this.endDate.set(hours ? date : addDays(date, 1));
    this.endTime.set(hours?.closesAt ?? '00:00');
  }

  protected async save(): Promise<void> {
    const category = this.category();
    if (!category || !this.rangeValid()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    try {
      await this.api.create(this.data.parkId, {
        cableId: this.where() === WHOLE_PARK ? null : this.where(),
        start: `${this.startDate()}T${this.startTime()}:00`,
        end: `${this.endDate()}T${this.endTime()}:00`,
        category,
        note: this.note().trim() || null,
      });
      this.ref.close(true);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}

/** Na celých 5 minut dolů: 14:07 → 14:05. Blokace „od teď“ nemá začínat v budoucnu. */
function roundDown(time: string): string {
  const [hours, minutes] = time.split(':').map(Number);
  return `${String(hours).padStart(2, '0')}:${String(minutes - (minutes % 5)).padStart(2, '0')}`;
}

/** Datum a čas posunuté o minuty, i přes půlnoc. */
function shift(date: string, time: string, minutes: number): { date: string; time: string } {
  const [hours, mins] = time.split(':').map(Number);
  const total = hours * 60 + mins + minutes;
  const days = Math.floor(total / (24 * 60));
  const rest = total - days * 24 * 60;
  return {
    date: addDays(date, days),
    time: `${String(Math.floor(rest / 60)).padStart(2, '0')}:${String(rest % 60).padStart(2, '0')}`,
  };
}
