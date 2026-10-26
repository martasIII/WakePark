# 0011 — Backoffice: Angular Material, typy z OpenAPI, rozložení podle rolí

**Stav:** Přijato · 26. 10. 2026

## Kontext

Backoffice (`wakepark-web`, Angular 22) má zatím přihlášení a seznam areálů, vlastní CSS bez
knihovny komponent a ručně psané typy odpovědí API. Teď přibydou obrazovky katalogu,
provozního kalendáře a blokací (ADR 0008, 0009): formuláře, tabulky, výběr data, dialogy,
a to jak na desktopu (admin), tak na mobilu u vody (obsluha).

## Rozhodnutí

- **Angular Material** jako knihovna komponent. Hotové formuláře, tabulky, výběr data v češtině,
  dialogy a přístupnost (klávesnice, čtečky). Vzhled zatím **neutrální pracovní**, barvy
  webu wake-dzban.cz se případně doplní později přes motiv.
- **Typy z OpenAPI, volání ručně.** `openapi-typescript` vygeneruje z popisu API jen typy
  (`src/app/core/api/schema.ts`, v gitu), volání se píšou ručně přes `httpResource` a `HttpClient`.
  Typy se tak nerozejdou s backendem a kód zůstane čitelný; generovaný klient (NSwag, Orval)
  by byl těžší a hůř laditelný. Generuje se skriptem z běžícího API ve vývoji.
- **Jedna aplikace, dva pohledy podle oprávnění** (ADR 0008): navigace ukazuje jen to, na co
  má uživatel oprávnění. Skutečnou kontrolu dělá vždy API.
- **Areál** se vybírá automaticky, když je jen jeden. Výběr se ukáže, až jich bude víc.
- **Jen čeština.** Texty produktů (cs/en) se editují vedle sebe.
- **Nasazení** na `admin.wake-dzban.cz` podle ADR 0010: kontejner s nginx servíruje build
  Angularu a `/api` přeposílá do API v síti Dockeru; nginx hostitele přidá doménu a TLS.

## Důsledky

**Co získáváme.** Rychlejší stavbu obrazovek, konzistentní ovládání a přístupnost bez vlastní
práce. Typovou kontrolu napříč backendem a frontendem.

**Co ztrácíme.** Material přidá do balíku aplikace desítky až stovky kB a vzhled je poznat jako
„Material“. Pro interní nástroj přijatelné.
