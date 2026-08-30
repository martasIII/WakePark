/**
 * Tvary, které vrací modul Tenancy.
 *
 * Zatím psané ručně. Až endpointů přibude, vygenerujeme je z `/openapi/v1.json`,
 * aby se kontrakt nemohl rozejít s backendem.
 */

/** Pravidla storna. Nastavuje si je každý areál sám. */
export interface CancellationPolicy {
  readonly cancellationAllowed: boolean;
  /** Kolik hodin před začátkem slotu lze stornovat bez sankce. */
  readonly freeCancellationHours: number;
  /** Kolik procent se vrací po uplynutí lhůty. */
  readonly lateCancellationRefundPercent: number;
  /** Kolik procent se vrací, když zákazník nedorazí. */
  readonly noShowRefundPercent: number;
}

/** Areál wakeparku. */
export interface Park {
  readonly id: string;
  readonly enterpriseId: string;
  readonly name: string;
  readonly description: string | null;
  readonly city: string;
  /** ISO 3166-1 alpha-2. */
  readonly countryCode: string;
  /** IANA identifikátor, např. `Europe/Prague`. */
  readonly timeZoneId: string;
  /** ISO 4217. */
  readonly currency: string;
  readonly cancellationPolicy: CancellationPolicy;
}
