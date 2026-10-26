import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import type { OperatingDaysImportDto } from '../../core/api/api';
import { num } from '../../core/api/api';
import { shortTime, weekdayName } from '../../shared/park-time';
import { problemMessage } from '../../shared/problem';
import { CalendarApi } from './calendar-api';

export interface ImportDialogData {
  readonly parkId: string;
}

const CHANGE_LABELS: Record<string, string> = {
  Added: 'nový',
  Changed: 'změna',
  Closed: 'zavřeno',
};

/**
 * Import provozní doby z CSV (ADR 0009): soubor nebo vložený text → náhled změn → uložit.
 * Nic se neuloží, dokud admin náhled nepotvrdí; s chybou v souboru se neuloží vůbec.
 */
@Component({
  selector: 'wp-import-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatProgressBarModule],
  templateUrl: './import-dialog.html',
  styleUrl: './import-dialog.scss',
})
export class ImportDialog {
  private readonly api = inject(CalendarApi);
  private readonly ref = inject<MatDialogRef<ImportDialog, OperatingDaysImportDto>>(MatDialogRef);
  private readonly data = inject<ImportDialogData>(MAT_DIALOG_DATA);

  protected readonly csv = signal('');
  protected readonly fileName = signal<string | null>(null);
  protected readonly preview = signal<OperatingDaysImportDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Jen dny, které se mění — nezměněné by jen zahltily náhled. */
  protected readonly changes = computed(() =>
    (this.preview()?.days ?? [])
      .filter((day) => day.change !== 'Unchanged')
      .map((day) => ({
        date: `${weekdayName(day.date)} ${Number(day.date.slice(8))}. ${Number(day.date.slice(5, 7))}.`,
        change: CHANGE_LABELS[day.change] ?? day.change,
        before: day.oldOpensAt ? `${shortTime(day.oldOpensAt)}–${shortTime(day.oldClosesAt)}` : 'zavřeno',
        after: day.newOpensAt ? `${shortTime(day.newOpensAt)}–${shortTime(day.newClosesAt)}` : 'zavřeno',
      })),
  );

  protected readonly summary = computed(() => {
    const p = this.preview();
    return p
      ? `${num(p.added)} nových, ${num(p.changed)} změněných, ${num(p.closed)} zavřených, ${num(p.unchanged)} beze změny`
      : '';
  });

  protected readonly canApply = computed(() => {
    const p = this.preview();
    return !!p && p.errors.length === 0 && this.changes().length > 0;
  });

  protected async readFile(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }

    this.fileName.set(file.name);
    this.setCsv(await file.text());
    await this.showPreview();
  }

  protected setCsv(text: string): void {
    this.csv.set(text);
    // Změněný text = starý náhled neplatí, jinak by se uložilo něco jiného, než admin viděl.
    this.preview.set(null);
  }

  protected async showPreview(): Promise<void> {
    await this.run(async () => this.preview.set(await this.api.previewImport(this.data.parkId, this.csv())));
  }

  protected async apply(): Promise<void> {
    await this.run(async () => this.ref.close(await this.api.applyImport(this.data.parkId, this.csv())));
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    try {
      await action();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
