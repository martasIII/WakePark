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
export type CreateBlockingRequest = Schemas['CreateBlockingRequest'];
export type CableDetailDto = Schemas['CableDetailDto'];
export type SetOperatingDayRequest = Schemas['SetOperatingDayRequest'];
export type OperatingDaysImportDto = Schemas['OperatingDaysImportDto'];
export type OperatingDayDiffDto = Schemas['OperatingDayDiffDto'];

// Přehled objednávek (ADR 0012)
export type OrderListDto = Schemas['OrderListDto'];
export type OrderListItemDto = Schemas['OrderListItemDto'];
export type OrderAdminDetailDto = Schemas['OrderAdminDetailDto'];
export type OrderVoucherDto = Schemas['OrderVoucherDto'];
export type UnmatchedPaymentDto = Schemas['UnmatchedPaymentDto'];

// Katalog a akční slevy (ADR 0009, 0014)
export type CatalogAdminDto = Schemas['CatalogAdminDto'];
export type AdminCategoryDto = Schemas['AdminCategoryDto'];
export type AdminProductDto = Schemas['AdminProductDto'];
export type AdminPromotionDto = Schemas['AdminPromotionDto'];
export type PromotionRequest = Schemas['PromotionRequest'];
