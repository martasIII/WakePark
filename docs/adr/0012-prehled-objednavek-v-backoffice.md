# ADR 0012 — Přehled objednávek e-shopu v backoffice

**Stav:** Přijato · 16. 11. 2026

## Kontext

E-shop běží naostro (od 16. 11. 2026). Objednávky, platby a vystavené poukazy jsou v databázi,
ale dívat se na ně jde jen dotazem v psql. Majitel a Dan potřebují stručný přehled: co se
prodalo, co čeká na platbu a které platby se nespárovaly.

## Rozhodnutí

**Jen čtení.** Sekce „Objednávky“ v backoffice: seznam s filtrem podle stavu a hledáním
(číslo objednávky, jméno, e-mail, firma), detail objednávky a seznam plateb k ručnímu
vyřízení. Žádné akce (ruční zaplacení, zrušení, nové odeslání e-mailu, PDF) — ty přijdou
v dalším kole podle toho, co v provozu opravdu chybí. Do té doby ručně v databázi.

**Hledání podle kódu voucheru ne** — ověřování kódů patří k pokladně (uplatnění), ne sem.

**Oprávnění `orders.read`** jen pro roli Admin (majitel a Dan). Manager ani Employee ho
nemají — přehled obsahuje osobní údaje zákazníků a údaje o platbách.

**API v modulu Billing** (`/api/v1/billing/admin/…`), provozovatel z přihlášení (ADR 0007),
omezení na provozovatele explicitně v každém dotazu. Detail objednávky doplní kódy poukazů
přes `Vouchers.Contracts` (`IVoucherDirectory`) — frontend volá jeden endpoint.

**Platby k ručnímu vyřízení** = pohyby ve stavu `Unmatched` (vypadají jako platba za
objednávku, ale nesedí VS nebo částka). Spárované a nesouvisející pohyby se neukazují.

## Důsledky

- Nové oprávnění doplní seeder při nasazení i do existující role Admin.
- Detail objednávky obsahuje přístupový klíč — z backoffice jde otevřít stránku objednávky
  tak, jak ji vidí zákazník.
- Akce nad objednávkami budou potřebovat vlastní oprávnění (`orders.manage`) a audit.
