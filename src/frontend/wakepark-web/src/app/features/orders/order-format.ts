import { num } from '../../core/api/api';

/**
 * Formátování pro přehled objednávek (ADR 0012). Časy v pásmu areálu Wake Park Džbán —
 * e-shop má zatím jediného provozovatele, ne podle vybraného areálu.
 */
const TIME_ZONE = 'Europe/Prague';

/** Veřejný web — odkaz na stránku objednávky, jak ji vidí zákazník. */
export const PUBLIC_WEB = 'https://wake-dzban.cz';

export const ORDER_STATUSES: readonly { readonly value: string; readonly label: string }[] = [
  { value: 'AwaitingPayment', label: 'Čeká na platbu' },
  { value: 'Paid', label: 'Zaplaceno' },
  { value: 'Cancelled', label: 'Zrušeno' },
];

export function orderStatusLabel(status: string): string {
  return ORDER_STATUSES.find((s) => s.value === status)?.label ?? status;
}

export function voucherStatusLabel(status: string): string {
  switch (status) {
    case 'Issued':
      return 'Platný';
    case 'Redeemed':
      return 'Uplatněný';
    case 'Cancelled':
      return 'Zrušený';
    default:
      return status;
  }
}

/** „1 980 Kč“ */
export function money(amount: number | string, currency: string): string {
  return new Intl.NumberFormat('cs-CZ', { style: 'currency', currency, maximumFractionDigits: 2, minimumFractionDigits: 0 }).format(
    num(amount),
  );
}

/** „16. 11. 2026 18:42“ */
export function dateTime(instant: string | null | undefined): string {
  if (!instant) {
    return '';
  }

  return new Intl.DateTimeFormat('cs-CZ', {
    timeZone: TIME_ZONE,
    day: 'numeric',
    month: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(instant));
}

/** `2026-11-16` → „16. 11. 2026“ */
export function date(isoDate: string | null | undefined): string {
  if (!isoDate) {
    return '';
  }

  const [y, m, d] = isoDate.split('-').map(Number);
  return `${d}. ${m}. ${y}`;
}
