import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { problemMessage } from '../../shared/problem';
import { CalendarApi } from './calendar-api';

export interface DayDialogData {
  readonly parkId: string;
  readonly date: string;
  /** `H:mm`, null = zavřeno. */
  readonly opensAt: string | null;
  readonly closesAt: string | null;
}

/** Otevírací doba jednoho dne, nebo zavřeno. Zavírá se `true`, když se uložilo. */
@Component({
  selector: 'wp-day-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatDialogModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ title }}</h2>

    <mat-dialog-content>
      <mat-checkbox [ngModel]="closed()" (ngModelChange)="closed.set($event)">Zavřeno</mat-checkbox>

      @if (!closed()) {
        <div class="times">
          <mat-form-field>
            <mat-label>Otevírá</mat-label>
            <input matInput type="time" [ngModel]="opensAt()" (ngModelChange)="opensAt.set($event)" required />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Zavírá</mat-label>
            <input matInput type="time" [ngModel]="closesAt()" (ngModelChange)="closesAt.set($event)" required />
          </mat-form-field>
        </div>
      }

      @if (error(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Zrušit</button>
      <button mat-flat-button type="button" [disabled]="!canSave() || saving()" (click)="save()">Uložit</button>
    </mat-dialog-actions>
  `,
  styles: `
    .times { display: flex; gap: 1rem; margin-top: 1rem; }
    .times mat-form-field { flex: 1; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class DayDialog {
  private readonly api = inject(CalendarApi);
  private readonly ref = inject<MatDialogRef<DayDialog, boolean>>(MatDialogRef);
  private readonly data = inject<DayDialogData>(MAT_DIALOG_DATA);

  protected readonly title = new Intl.DateTimeFormat('cs-CZ', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC' }).format(
    new Date(`${this.data.date}T00:00:00Z`),
  );

  protected readonly closed = signal(this.data.opensAt === null);
  protected readonly opensAt = signal(pad(this.data.opensAt) ?? '10:00');
  protected readonly closesAt = signal(pad(this.data.closesAt) ?? '19:00');
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly canSave = computed(() => this.closed() || (this.opensAt() !== '' && this.closesAt() > this.opensAt()));

  protected async save(): Promise<void> {
    this.saving.set(true);
    this.error.set(null);

    try {
      await this.api.setDay(
        this.data.parkId,
        this.data.date,
        this.closed() ? null : this.opensAt(),
        this.closed() ? null : this.closesAt(),
      );
      this.ref.close(true);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}

/** `9:30` → `09:30` pro `<input type="time">`. */
function pad(time: string | null): string | null {
  return time ? time.padStart(5, '0') : null;
}
