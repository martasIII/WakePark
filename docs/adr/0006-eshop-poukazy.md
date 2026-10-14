# 0006 — E-shop s poukazy: moduly, platba QR převodem, veřejný web

**Stav:** Navrženo · 6. 10. 2026 — čeká na rozhodnutí otevřených otázek

## Kontext

Web wake-dzban.cz (WordPress + WooCommerce) byl v červenci 2026 napaden a stažen. Nahradí ho
vlastní řešení postavené na WakeParku, ne reinstalace WordPressu.

Rozsah:

- veřejné stránky (úvod, ceník, kontakt, provozní řád, obchodní podmínky …), obsah edituje
  jen vlastník, administrace obsahu je **mimo rozsah**,
- e-shop: 3 kategorie, řádově 30 položek, **výhradně elektronické poukazy v PDF**,
- platba **jen QR převodem**, příchozí platby se párují přes API Fio banky,
- poukaz se uplatní **kódem na místě**, bez vazby na rezervační systém,
- hosting na stávajícím Wedos VPS (Debian 12), kde už běží Docházka,
- termín: prodej vánočních poukazů, tedy ostrý provoz nejpozději v polovině listopadu.

Datový model už počítá s moduly `Catalog` (produkty, ceníky) a `Billing` (objednávky, platby)
a `Notifications`. E-shop je jejich první reálný konzument.

## Rozhodnutí

### Moduly — žádný modul „Shop"

E-shop není samostatná doména, je to kanál prodeje. Staví se z modulů, které datový model už
popisuje, plus jednoho nového:

| Modul | Co přináší e-shop | Stav |
|---|---|---|
| `Catalog` | `Product`, `PriceList`, `PriceItem` + nově `ProductCategory` | popsán, neimplementován |
| `Billing` | `Order`, `OrderLine`, `Payment` + nově `BankTransaction`, daňový doklad | popsán, neimplementován |
| `Vouchers` | `Voucher` — kód, platnost, stav, uplatnění | **nový** |
| `Notifications` | e-mail s poukazy a dokladem v příloze | popsán, neimplementován |

`Vouchers` je samostatný modul, protože má vlastní životní cyklus (vystaven → uplatněn / propadl)
nezávislý na objednávce a v budoucnu ho bude chtít číst i `Reservations` (uplatnění poukazu
při rezervaci). Kdyby žil v `Billing`, musel by `Reservations` sahat do fakturace.

`Customers` se **neimplementuje**. Objednávka si zamrazí kontakt kupujícího (jméno, e-mail,
volitelně fakturační údaje) stejně jako `OrderLine` zamrazuje cenu. `Order.CustomerId` zůstává
volitelný pro pozdější napojení.

### Tok objednávky

```
Storefront          Billing                         Vouchers        Notifications
    │ checkout         │                                │                │
    ├────────────────► Order: AwaitingPayment           │                │
    │ ◄─ QR (SPAYD) ── VS = číslo objednávky            │                │
    │                  │                                │                │
    │       FioPollingWorker ─► BankTransaction (inbox, UNIQUE fio_id)   │
    │                  │ párování VS + částka           │                │
    │                  ├─► Payment: Succeeded           │                │
    │                  ├─► Order: Paid + daňový doklad  │                │
    │                  └─► outbox: OrderPaid ──────────► Voucher × N     │
    │                                                   └─► outbox: VouchersIssued ─► e-mail + PDF
```

- **QR platba** ve formátu SPAYD: účet (IBAN z `Enterprise`), částka, `CZK`, variabilní symbol,
  zpráva pro příjemce. Variabilní symbol = číslo objednávky (max. 10 číslic).
- **Fio API** se dotazuje pravidelně workerem na pozadí. Token **jen pro čtení**, mimo repo.
  Limit volání API se respektuje ve workeru, ne doufáním.
- **`BankTransaction` je inbox.** Každý pohyb se uloží právě jednou (UNIQUE na ID pohybu
  ve Fio), teprve pak se páruje. Opakované stažení výpisu tak nikdy nevystaví druhý poukaz.
- **Nespárované platby** (chybný VS, nedoplatek, přeplatek, platba po vypršení objednávky)
  se nepárují automaticky. Skončí ve frontě k ručnímu vyřízení v backoffice.
- **Mezi moduly vede outbox**, ne přímé volání. Událost se zapíše do tabulky outbox ve stejné
  transakci jako změna stavu, dispatcher ji doručí a příjemce je idempotentní. Pád procesu
  mezi zaplacením a vystavením poukazu tak nevede ke ztrátě ani zdvojení.

### Veřejný web

Samostatný projekt `WakePark.Storefront` (**ASP.NET Core Razor Pages**), generovaný na serveru
kvůli vyhledávačům, minimum JavaScriptu. S backendem mluví **jen přes HTTP API** přes gateway,
stejně jako Angular — žádná reference na moduly.

Obsahové stránky jsou markdown soubory nasazené s aplikací. Změna textu = commit a nasazení.

*Doplněno 7. 10. 2026:* ceník placený na místě a otevírací doba jsou JSON v
`WakePark.Storefront/data/` — v gitu, ne mimo aplikaci na serveru. Vlastník je mění často
a ručně; testy obsah validují, takže chyba v JSONu neprojde CI a na web se nedostane,
a git drží historii změn. Cenou je, že změna ceny vyžaduje commit (lze i přes web GitHubu).

Angular (`wakepark-web`) zůstává pro backoffice: objednávky, nespárované platby, ověření
a uplatnění poukazu.

### Nasazení

`docker compose` na Wedos VPS: PostgreSQL, API, Gateway, Storefront a reverse proxy s TLS
(Caddy). Build a nasazení z GitHub Actions.

## Důsledky

**Co získáváme.** E-shop rovnou staví moduly, které systém stejně potřebuje. Párování plateb
je reálný důvod pro outbox a idempotenci, ne cvičení. Žádný poplatek platební bráně.

**Co ztrácíme.** Platba není okamžitá — poukaz dorazí až po připsání převodu (u Fio zpravidla
v řádu minut, z jiné banky i další pracovní den). Na vánoční nákup na poslední chvíli to je
nevýhoda a musí to být napsané u pokladny. Nespárované platby vyžadují ruční práci.

Do produkce půjde i část WakeParku, která e-shop nepotřebuje (Identity, Tenancy). Rezervační
endpointy se v produkci nepublikují, dokud nejsou hotové.

**Kdy to přehodnotit.** Až bude potřeba okamžitá platba kartou — pak platební brána jako další
`Payment.Method`, model objednávky se nemění.

## Zvažované alternativy

**Reinstalace WordPress + WooCommerce.** Zamítnuto vlastníkem po napadení: nedůvěra
k ekosystému pluginů a cena průběžné bezpečnostní údržby.

**Hostovaný e-shop (Shoptet apod.).** Nejrychlejší cesta, ale měsíční poplatek a poukazy by
se nedaly později napojit na rezervace.

**Samostatná malá aplikace mimo WakePark.** Rychlejší start, ale stejné entity (produkt,
objednávka, platba) by vznikly dvakrát.

**Angular i pro veřejný web (SSR).** Zamítnuto: pro pár stránek a 30 položek zbytečný
JavaScript a Node runtime v produkci navíc.

## Upřesnění z 6. 10. 2026

**Poukaz** je jednorázový, na konkrétní produkt pro jednu nebo více osob (např. „Voucher pro
dvě osoby“). Počet osob se zamrazí do poukazu z produktu. Uplatní se celý najednou, žádné
postupné čerpání.

**Platnost** — navrženo: *do konce první sezóny, která začne po datu nákupu*; v probíhající
sezóně platí i zbytek té. Vánoční poukaz 2026 tedy platí celou sezónu 2027, poukaz koupený
v červenci 2026 zbytek sezóny 2026 i celou 2027. Konec sezóny se čte z `Operations.Season`;
když příští sezóna ještě není založená, použije se výchozí datum z konfigurace areálu.
Datum se do poukazu **zamrazí** při vystavení — pozdější změna sezóny ho nezkrátí.

**DPH** — dosavadní praxe: daňový doklad hned po přijetí platby. Odpovídá jednoúčelovému
poukazu (místo plnění i sazba jsou při prodeji známé), DPH se odvádí při úhradě, uplatnění
poukazu už doklad nevytváří. **Před úhradou se daňový doklad nevystavuje** — zákazník dostane
jen potvrzení objednávky s QR kódem (není to doklad). Ověřit s účetní.

**Splatnost** — 7 kalendářních dní, 4. den upomínka s QR kódem, 8. den zrušení. Pozdní platba
jde do fronty nespárovaných.

**Číselné řady** — dvě, obě na `Enterprise`, roční reset:

| Řada | Formát | Mezery | Vzniká |
|---|---|---|---|
| Objednávka | `2026000123` (10 číslic = VS) | povolené (propadlé objednávky) | při odeslání objednávky |
| Daňový doklad | `FV2026-00001` | **bez mezer** | při zaplacení, ve stejné transakci, ze zamčeného čítače |

**E-mail** — SMTP Wedosu z adresy domény, na doméně SPF, DKIM a DMARC. Záloha: transakční
služba (Brevo apod.).

**Prodávající** je Wake Park s.r.o. (IČO 06633633, plátce DPH) — na daňovém dokladu,
v obchodních podmínkách i jako majitel Fio účtu. Údaje se berou z `Enterprise`.

## Jazyky (7. 10. 2026)

**Povinně čeština a angličtina**, volitelně němčina, francouzština a španělština. Statický web
zůstává jen česky, vícejazyčnost přijde se Storefrontem.

- **Adresy:** čeština v kořeni (`/cenik`), ostatní jazyky s prefixem (`/en/cenik`). Na každé
  stránce `hreflang` a přepínač jazyka. Jazyk se nepřepíná JavaScriptem na jedné adrese —
  vyhledávače by viděly jen jeden.
- **Data (JSON):** jeden soubor, přeložené jsou jen texty: `{"cs": "…", "en": "…"}`; prostý
  řetězec platí pro všechny jazyky. Chybějící překlad se zobrazí česky. Ceny jsou jen jednou.
  Statický web už tento formát používá, Storefront ho převezme beze změny.
- **Storefront:** `RequestLocalization` s jazykem z adresy, texty rozhraní v `.resx`, obsahové
  stránky jako markdown pro každý jazyk.
- **E-shop:** přeložené názvy a popisy produktů v `Catalog`; `Order` si uloží jazyk zákazníka,
  podle něj se posílá e-mail, PDF poukaz a upomínka. Daňový doklad česky.
- **Překlady** navrhuje AI. EN a FR zkontroluje vlastník nebo rodilý mluvčí, DE a ES zůstanou
  bez kontroly — proto **obchodní podmínky a pokladna jen v CS/EN/FR**; DE/ES jen informativní
  stránky s odkazem na anglickou verzi podmínek.
- Rezervační systém r2.wakearealy.cz má jen češtinu; cizojazyčné stránky na to upozorní.

## Otevřené otázky

1. Potvrdit s účetní režim jednoúčelového poukazu.
