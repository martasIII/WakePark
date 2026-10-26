/**
 * Datum a čas v časovém pásmu areálu, ne prohlížeče — obsluha v cizině nebo počítač se
 * špatně nastaveným pásmem musí vidět stejné časy jako areál.
 */

/** `YYYY-MM-DD` daného okamžiku v pásmu areálu. */
export function localDate(instant: string | Date, timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(
    new Date(instant),
  );
}

/** `H:mm` daného okamžiku v pásmu areálu. */
export function localTime(instant: string | Date, timeZone: string): string {
  return new Intl.DateTimeFormat('cs-CZ', { timeZone, hour: 'numeric', minute: '2-digit' }).format(new Date(instant));
}

/** `10:00:00` z API → `10:00`. */
export function shortTime(time: string | null | undefined): string {
  if (!time) {
    return '';
  }

  const [hours, minutes] = time.split(':');
  return `${Number(hours)}:${minutes}`;
}

/** Dny měsíce jako `YYYY-MM-DD` — počítané bez časového pásma, ať se nic neposune. */
export function daysOfMonth(year: number, month: number): string[] {
  const count = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return Array.from({ length: count }, (_, i) => isoDate(year, month, i + 1));
}

export function isoDate(year: number, month: number, day: number): string {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

/** Den v týdnu 0 = pondělí … 6 = neděle. */
export function weekdayIndex(date: string): number {
  const [y, m, d] = date.split('-').map(Number);
  return (new Date(Date.UTC(y, m - 1, d)).getUTCDay() + 6) % 7;
}

/** Den v týdnu krátce česky: „po“, „út“ … */
export function weekdayName(date: string): string {
  return ['po', 'út', 'st', 'čt', 'pá', 'so', 'ne'][weekdayIndex(date)];
}
