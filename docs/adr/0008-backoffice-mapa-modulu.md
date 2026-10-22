# 0008 — Backoffice: mapa modulů, dva pohledy a pořadí

**Stav:** Navrženo · 22. 10. 2026

## Kontext

Web wake-dzban.cz a e-shop s poukazy běží (ADR 0006). Všechno ostatní se dnes dělá ručně
nebo mimo WakePark:

- ceník a otevírací doba jsou v JSON souborech webu,
- docházka brigádníků běží jako samostatná aplikace (Docházka, .NET 8 + SQLite),
- mzdové podklady zpracovává Martin ručně,
- prodej na místě nemá systém.

Cíl je **jeden systém z modulů**: web, e-shop, rezervace, pokladna, provoz, personál
i docházka nad jednou databází a jedním přihlášením.

Rámcová podmínka: **sezóna 2027 je pilotní provoz pro jediný park (Wake Park Džbán).**
Obecné řešení pro další provozovatele se začne připravovat až po sezóně, s poučením
z pilotu — i za cenu pozdějšího přepracování. Rozkročit se obecně hned je zkušenost
z jiných projektů, které na tom skončily.

## Rozhodnutí

### Moduly

| Oblast | Modul | Stav | Kdo |
|---|---|---|---|
| Sezóna, vleky, provozní doba (i podle slunce), blokace | **Operations** | modul je, chybí UI | admin |
| Produkty, kategorie + podkategorie, ceny | **Catalog** | je (bez podkategorií) | admin |
| Rezervace slotů | **Reservations** | modul je, chybí UI | zákazník online, obsluha, admin |
| Prodej na místě, pokladna, uplatnění voucheru, EET | **Sales** | nový | obsluha, admin |
| Doklady, tržby, podklady pro účetní (DPH, KH) | **Billing** | e-shopová část hotová | admin |
| Uplatnění a stav voucherů | **Vouchers** | vydávání hotové | obsluha, admin |
| Brigádníci, nástup / ukončení, limity DPP/DPČ, podklady ke mzdám | **Staff** | nový | admin |
| Docházka (příchod / odchod, opravy, reporty) | **Attendance** | převzít z Docházky | obsluha, admin |
| Plánování směn | **Scheduling** | nový | admin plánuje, obsluha se hlásí |
| Uživatelé, role, oprávnění | **Identity** | je | — |

**Jedna pravda o cenách a provozu.** Ceník na webu, produkty v e-shopu i tlačítka pokladny
čtou **Catalog**; otevírací dobu na webu a volné sloty určuje **Operations**. Změna
v backoffice se hned projeví všude. `cenik.json` a `oteviraci-doba.json` z webu zmizí.

### Dva pohledy, jedna aplikace

Backoffice je jedna Angular aplikace (`wakepark-web`), společné přihlášení, co kdo vidí
určují role:

- **Admin** — plný backoffice, primárně desktop.
- **Obsluha** (brigádníci) — zjednodušený, **mobile-first** (mobil u vody, desktop na
  recepci): pokladna, rezervace dne, docházka, moje směny, přehled pokladny.

Jedna aplikace = sdílené modely a komponenty, dva pohledy se nerozejdou.

**Rezervace pro zákazníky** jsou veřejná část webu — Storefront, graficky jednotný
s wake-dzban.cz, ne backoffice.

### Pokladna (Sales)

- Platba **hotově** nebo **kartou**. Karta = samostatný terminál, v systému se jen označí
  „zaplaceno kartou“; terminál tiskne potvrzení o transakci sám. Integrace s terminálem
  není potřeba.
- Daňový doklad na **účtenkové tiskárně na recepci** (desktop). Na mobilu se prodá,
  tiskne se na recepci — vždy, nebo na vyžádání podle pravidel EET 2.0 (níže).
- Přehled pokladny (hotovost v kase, tržba dne po způsobech platby) vidí i obsluha.

### Rezervace

Zákazníci rezervují primárně sami online. Rezervační frontend je veřejný, přístupný
(a11y) a vypadá jako zbytek webu.

### Docházka

Převzít do WakeParku (modul Attendance): příchod / odchod pro obsluhu, reporty a ruční
opravy jen pro admina. **Nízká priorita** — dnešní Docházka do té doby běží dál.

### Personál a mzdy (Staff)

Cíl: Martin nezpracovává mzdy ručně. Účetní dostane za měsíc **report za každého
brigádníka**: odpracované hodiny, hodinovou sazbu a příznak podepsaného prohlášení
poplatníka. Systém **hlídá limity hodin DPP/DPČ včetně pravidel pro nezletilé**. JMHZ
a kdo co vyplňuje — otevřené (viz níže).

### Směny (Scheduling)

Pravidla zatím volná, upřesní se. Pevný požadavek: **admin může pravidlo jednorázově
obejít** (výjimka se zaznamená, kdo a proč).

### Multi-tenant

Pilot pro jeden park. Platí:

- **Zůstává, co už je:** `EnterpriseId` na datech a omezení dotazů na provozovatele
  (ADR 0007). Je to levné a hlídá to bezpečnost.
- **Nestaví se:** konfigurovatelná pravidla „pro jakýkoli park“, obecné enginy
  (provozní doba, směny, mzdy), administrace provozovatelů. Pravidla Wake Parku se
  napíšou napřímo a srozumitelně; zobecní se po sezóně, až budou známá.

## Pořadí

1. **Do sezóny 2027 nutně:**
   - Operations (sezóna, vleky, provozní doba) + Catalog v backoffice → web a e-shop
     z databáze,
   - pokladna s **uplatněním voucherů** (prodané přes Vánoce se musí dát přijmout)
     a **EET 2.0**.
2. **Do sezóny, pokud to vyjde:** přehledy tržeb a pokladny, převzetí docházky.
3. **Nice to have:** online rezervace pro zákazníky — nejdřív odhad práce a nevýhod.
4. **Po rozjezdu:** podklady pro účetní (DPH, KH), Staff, Scheduling, JMHZ.

Každá oblast dostane vlastní ADR (jako e-shop), kód až po něm.

## Otevřené otázky

1. **EET 2.0 (zákon č. 180/2026 Sb., účinnost 1. 1. 2027)** — eviduje se hotovost
   i karta na místě; převody a e-shop ne. Rozhodnout: integrovat do pokladny
   (pokladní certifikát z DIS+, technická specifikace FS), nebo v pilotu evidovat ručně
   bezplatnou aplikací MOJE eet. Ruční evidence = žádný vývoj a změna legislativy se
   nepromítá do systému, ale dvojí zadávání u každého prodeje a riziko chyby. Upřesnit
   i povinnost tisku účtenky (podle prvních shrnutí jen na žádost nebo elektronicky).
   Ověřit u zdroje (Finanční správa) a s účetní.
2. **Mzdy a JMHZ** — co dělá systém a co účetní. Martin rozmyslí.
3. **Pravidla směn** — doplní Martin.
4. **Online rezervace** — stojí za to do sezóny 2027? Odhad po návrhu Operations.
5. **Historie docházky** — přenést z Docházky, nebo začít čistě?

## Důsledky

**Co získáváme.** Jedno místo pro ceny a provoz; obsluha i admin v jedné aplikaci;
podklady pro účetní bez ručního přepisování. Pilot bez předčasného zobecňování je
rychlejší a pravidla vzniknou podle skutečného provozu.

**Co ztrácíme.** Po sezóně se část kódu bude kvůli dalším parkům přepisovat. Pokladna
s EET je nový, právně citlivý kus, který musí být hotový k 1. 5. 2027 — ostatní priority
se mu podřídí.
