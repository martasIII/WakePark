import type { components } from './schema';

/**
 * Typy odpovědí a požadavků API. Generují se z `/openapi/v1.json` (`npm run api:types`,
 * ADR 0011), takže se nemůžou rozejít s backendem — ručně se tu jen pojmenovávají.
 */
export type Schemas = components['schemas'];

export type CurrentUserDto = Schemas['CurrentUserDto'];
export type ParkAccessDto = Schemas['ParkAccessDto'];
export type ParkDto = Schemas['ParkDto'];

/**
 * Desetinné číslo z API. .NET ho v OpenAPI popisuje jako `number | string` (umí číst obojí),
 * po drátě ale posílá číslo. Tady se sjednotí na `number`.
 */
export function num(value: number | string | null | undefined): number {
  return value === null || value === undefined ? Number.NaN : Number(value);
}

export type OperatingCalendarDto = Schemas['OperatingCalendarDto'];
export type OperatingDayDto = Schemas['OperatingDayDto'];
export type BlockingDto = Schemas['BlockingDto'];
export type CableDetailDto = Schemas['CableDetailDto'];
export type SetOperatingDayRequest = Schemas['SetOperatingDayRequest'];
export type OperatingDaysImportDto = Schemas['OperatingDaysImportDto'];
export type OperatingDayDiffDto = Schemas['OperatingDayDiffDto'];
