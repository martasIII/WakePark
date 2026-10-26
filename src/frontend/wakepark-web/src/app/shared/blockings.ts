/** Důvody blokace, jak je zná API (ADR 0009). Na webu se ukáže jen důvod, nikdy poznámka. */
export interface BlockingCategory {
  readonly value: string;
  /** Do formuláře a seznamu blokací. */
  readonly label: string;
  /** Do políčka dne v kalendáři, kde není místo. */
  readonly short: string;
}

export const BLOCKING_CATEGORIES: readonly BlockingCategory[] = [
  { value: 'TechnicalFault', label: 'Technická závada', short: 'závada' },
  { value: 'PrivateEvent', label: 'Soukromá akce', short: 'akce' },
  { value: 'Weather', label: 'Počasí', short: 'počasí' },
  { value: 'Other', label: 'Jiný důvod', short: 'mimo provoz' },
];

export function blockingCategory(value: string): BlockingCategory {
  return BLOCKING_CATEGORIES.find((category) => category.value === value) ?? { value, label: value, short: value };
}
