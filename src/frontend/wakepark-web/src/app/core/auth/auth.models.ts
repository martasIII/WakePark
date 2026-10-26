import type { CurrentUserDto, ParkAccessDto } from '../api/api';

/** Přístup uživatele do jednoho areálu. */
export type ParkAccess = ParkAccessDto;

/** Kdo je přihlášený a co smí. `globalPermissions` platí ve všech areálech provozovatele. */
export type CurrentUser = CurrentUserDto;

/** Kódy oprávnění. Musí odpovídat konstantám v Identity.Contracts na backendu. */
export const Permissions = {
  parksRead: 'parks.read',
  parksManage: 'parks.manage',
  operationsRead: 'operations.read',
  operationsManage: 'operations.manage',
  blockingsManage: 'blockings.manage',
  reservationsRead: 'reservations.read',
  reservationsCreate: 'reservations.create',
  reservationsCancel: 'reservations.cancel',
  passesManage: 'passes.manage',
  usersManage: 'users.manage',
  catalogManage: 'catalog.manage',
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];
