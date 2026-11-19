# ADR 0013 — Testovací prostředí UAT s kopií ostrých dat

**Stav:** Přijato · 19. 11. 2026

## Kontext

Web a e-shop běží naostro. Další změny (akční slevy, později pokladna) sahají do databáze a
majitel s Danem je chtějí před nasazením vyzkoušet na skutečných datech, z prohlížeče, se
stejnými účty jako v backoffice. Nikdo jiný se do testovací verze dostat nesmí.

## Rozhodnutí

**Samostatná kopie, ne nový kód nad produkční databází.** Sdílená databáze by znamenala, že
migrace z testované verze změní schéma produkce, úlohy na pozadí (Fio, upomínky, outbox)
běží dvakrát a e-maily z testů odcházejí zákazníkům.

- **Vlastní databázový kontejner** (`wakepark-uat-db`) i síť Dockeru. Data jsou kopie noční
  zálohy produkce, nahraná na požádání skriptem `uat-refresh.sh` (případně s novou zálohou,
  `--fresh`). Účty a hesla tak přijdou s daty.
- **Adresy:** web `uat.wake-dzban.cz`, backoffice `admin.uat.wake-dzban.cz`. Přihlašovací
  cookie má v UAT doménu `uat.wake-dzban.cz` (`Auth:CookieDomain`), takže ji dostane i web.
  Produkční cookie (jen pro `admin.wake-dzban.cz`) do UAT nedoletí a naopak.
- **Přístup jen pro Admina:** nové oprávnění `uat.access` (jen role Admin). Nginx před webem
  UAT se při každém požadavku ptá `GET /api/v1/identity/uat-access` (`auth_request`);
  nepřihlášeného pošle na přihlášení do backoffice UAT. Backoffice hlídá přihlášení sám.
- **Vypnuté vedlejší účinky:** párování plateb z Fio (UAT nemá token), upomínky a rušení
  nezaplacených objednávek (`Billing:ProcessUnpaidOrders = false`). E-maily jdou jen
  správcům (`Notifications:RedirectTo`) s původním adresátem v předmětu
  „[UAT → jan@…] …“, odkazy v nich vedou na UAT.
- **Označení:** žlutý pruh na každé stránce webu (`Storefront:EnvironmentLabel`), štítek
  v backoffice, `noindex` pro vyhledávače.
- **Nasazování:** větev `uat` → CI (stejné testy a obrazy) → `deploy.sh uat <commit>`.
  Stejný omezený SSH klíč jako produkce, skript podle argumentu zvolí adresář.

## Důsledky

- Na serveru navíc asi 370 MB paměti (z 1,9 GB). Když bude těsno, UAT jde zastavit
  (`docker compose stop` v `/opt/wakepark/uat`) bez vlivu na produkci.
- Kopie dat obsahuje osobní údaje zákazníků. Zůstává na stejném serveru, u stejného
  správce, přístupná jen Adminům — mimo server se nekopíruje.
- Objednávka v UAT ukazuje skutečný účet pro platbu (QR). Testovací objednávky se neplatí;
  platba by v UAT stejně nikdy nespárovala.
- Data v UAT stárnou; před testem změny, která na datech záleží, spustit `uat-refresh.sh`.
