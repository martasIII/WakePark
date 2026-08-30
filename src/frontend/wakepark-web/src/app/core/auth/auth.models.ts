/** Přístup uživatele do jednoho areálu. */
export interface ParkAccess {
  readonly wakeParkId: string;
  readonly roles: readonly string[];
  readonly permissions: readonly string[];
}

/** Kdo je přihlášený a co smí. */
export interface CurrentUser {
  readonly userAccountId: string;
  readonly email: string;
  readonly enterpriseId: string | null;
  readonly parks: readonly ParkAccess[];
  /** Oprávnění platná ve všech areálech provozovatele. */
  readonly globalPermissions: readonly string[];
}

/** Kódy oprávnění. Musí odpovídat konstantám v Identity.Contracts na backendu. */
export const Permissions = {
  parksRead: 'parks.read',
  parksManage: 'parks.manage',
  operationsRead: 'operations.read',
  operationsManage: 'operations.manage',
  reservationsRead: 'reservations.read',
  reservationsCreate: 'reservations.create',
  reservationsCancel: 'reservations.cancel',
  passesManage: 'passes.manage',
  usersManage: 'users.manage',
} as const;
