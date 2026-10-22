# 0006 — E-shop s poukazy: moduly, platba QR převodem, veřejný web

**Stav:** Navrženo · 6. 10. 2026, upřesněno 21. 10. 2026 — čeká na rozhodnutí otevřených otázek

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
| Daňový doklad | `FV_E_2026-000001` (upřesněno 22. 10.) | **bez mezer** | při zaplacení, ve stejné transakci, ze zamčeného čítače |

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

## Upřesnění z 21. 10. 2026

Mění část upřesnění z 6. 10. (splatnost, číselná řada objednávky, e-mail).

**Sortiment** — pro e-shop je produkt jen **název, popis, obrázek, cena, sazba DPH a kategorie**
(„Vouchery“, „Permanentky“). Žádné druhy, sloty ani počty jízd — co produkt na místě znamená
(1/2/3 sloty, kolik jízd má permanentka), se doplní s backoffice. Pro e-shop je i permanentka
poukaz: zaplatí se, vystaví se PDF s kódem.

**Poukaz** — kód, platnost a **zamrazený produkt** (odkaz na produkt + název, popis, cena a DPH
k datu nákupu). Až backoffice doplní k produktům sloty a jízdy, prodané poukazy se k nim
přiřadí přes produkt, i když se mezitím změní název nebo cena.

**Platnost** — prozatím pevné období z konfigurace areálu, pro poukazy prodané teď
**1. 5. 2027 – 30. 9. 2027**. Sezóna může být delší, ale neslibuje se kvůli vodě a počasí.
Do poukazu se zamrazí při vystavení. Nahrazuje pravidlo „do konce první sezóny po nákupu“
z 6. 10. — k tomu se vrátí, až bude `Operations.Season`.

**Podoba PDF** — navrhne grafik (Dan), poukazy 1/2/3 budou barevně odlišené. Do té doby
obecná šablona; barva a podklad patří k produktu, ne do kódu. Obsah (číslo dokladu, kód, …)
se dořeší při proklikávání.

**Hodnotový poukaz (na částku) se neprodává.** Byl by to víceúčelový poukaz — DPH až při
uplatnění, při prodeji jen potvrzení o platbě, ne daňový doklad. Druhý daňový režim v `Billing`
za „nice to have“ nestojí. Kdyby se vrátil, je to nový druh produktu a nový režim, ne úprava
stávajícího.

**Backoffice do Vánoc není.** E-shop poukazy a permanentky jen prodá a vystaví. Ověření kódu,
uplatnění poukazu a strhávání jízd z permanentky musí být hotové **před začátkem sezóny 2027** —
do té doby se nic neuplatňuje. Do té doby musí být poukazy a permanentky dohledatelné aspoň
přímým dotazem do databáze.

**Splatnost** — `Billing:PaymentDueDays`, výchozí **4 dny**, upomínka s QR kódem po 2 dnech.
**Pozdní platba** se správným VS a přesnou částkou se přijme automaticky (objednávka se obnoví,
vystaví se doklad i poukaz) — poukazů není omezený počet, takže zrušení nic nechrání, a přijatou
úplatu by stejně bylo nutné vyfakturovat nebo vrátit. Do fronty k ručnímu vyřízení jde jen
nesoulad VS nebo částky.

**Číslo objednávky** — prozatím dvojčíslí roku + pořadové číslo od 1000 (`261000`), roční reset.
Definitivní formát zůstává otevřený. Čísla jdou po sobě, proto stránka objednávky (QR, stav)
je dostupná jen přes náhodný token v adrese, nikdy jen přes číslo.

**E-mail** — odesílá `noreply@wake-dzban.cz` s `Reply-To: info@wake-dzban.cz`; heslo v
konfiguraci aplikace tak není heslo k pracovní schránce. Ve vývoji Mailpit, schránku na Wedosu
zřídí vlastník před ostrým provozem. Heslo k info@ změnit (mohlo ležet v napadené DB WordPressu).

## Objednávka a DPH (21. 10. 2026)

**DPH** — účetní potvrdila: voucher obsahující vstup do areálu, čas na vleku, vybavení
a trenéra (jako vedlejší plnění, ne hlavní) je **jednoúčelový poukaz se sazbou 12 %**.
Totéž platí pro permanentky. Daňový doklad se tedy vystavuje při zaplacení.

**Objednávka bez košíku.** Na stránce e-shopu je u každého produktu počet kusů, pod nimi
kontakt a tlačítko „Objednat“ — jeden formulář, bez JavaScriptu a bez cookie. Při 4–10
produktech by košík byl zbytečná složitost. Cenu počítá vždy server z katalogu.

**Zákazník** — jméno a e-mail povinné, telefon nepovinný; volitelně firma, IČO a DIČ
(daňový doklad na firmu). Později: dohledání a ověření subjektu v ARES podle IČO.

**Souhlas s obchodními podmínkami** je povinný. Podmínky a zásady ochrany osobních údajů
se píšou znovu a zreviduje je advokátka — **bez nich se ostrý prodej nespouští**.

**Platební účet** — Fio 2601338268/2010 (IBAN CZ95 2010 0000 0026 0133 8268) zatím
v konfiguraci, ne v `Enterprise`: tabulka provozovatelů v produkci ještě není a bez backoffice
by nešla upravit. Přesune se s backoffice.

**Objednávkový endpoint API je anonymní.** Dnes to nevadí — API nemá publikovaný port
a objednávku umí založit jen web (ten hlídá počet objednávek z IP, skryté pole proti
robotům a strop na kusy). **Než se API otevře ven přes gateway**, musí endpoint dostat
vlastní ochranu (klíč webu, rate limit).

## Párování plateb z Fio (22. 10. 2026)

- **Stahování za 7 dní zpětně při každém běhu** (`/periods/…`), ne „od poslední zarážky“
  (`/last/…`). Zarážka se v bance posune už stažením — pád mezi stažením a uložením by pohyby
  ztratil. Opakované stažení je bezpečné díky inboxu (UNIQUE na ID pohybu); výpadek serveru
  až na týden se srovná sám.
- **Interval 5 minut**, worker v procesu API. Chyba jednoho běhu (Fio nedostupné) se zaloguje,
  další běh to zkusí znovu.
- **Nejdřív se uloží inbox, pak se páruje.** Zaplacení objednávky a označení pohybu jsou
  v jedné transakci.
- **Páruje se jen jistota:** VS = číslo objednávky a přesná částka. Nedoplatek, přeplatek,
  neexistující objednávka a druhá platba za zaplacenou objednávku jdou k ručnímu vyřízení.
- **Na účet chodí i jiné platby** — za číslo objednávky se považuje jen VS se 6–8 číslicemi,
  který začíná letošním nebo loňským dvojčíslím roku. Ostatní pohyby jsou „mimo e-shop“
  a **nenechávají se u nich osobní údaje** (jméno, účet, zpráva) — jen ID, datum, částka a VS.
- **Token** (jen „Sledovat účet“) je pouze v `.env` na serveru. Je součástí adresy dotazu,
  proto HttpClient Fio nic neloguje a chyby se přebalují bez adresy.

## Outbox a e-maily (22. 10. 2026)

- **Outbox v Billingu:** doménové události objednávky (`OrderPlaced`, `OrderPaid`) se při
  `SaveChanges` převedou na integrační události a zapíšou do `billing.outbox_messages` **ve
  stejné transakci** jako objednávka. Obecné části (zpráva, mapování, dispatcher) jsou
  v `BuildingBlocks`, další moduly je použijí stejně.
- **Dispatcher** v procesu API doručuje každých 5 s „aspoň jednou“, s rostoucím odstupem
  při chybě. Počítá s jednou instancí (pro víc instancí `FOR UPDATE SKIP LOCKED`).
- **Události jsou tenké** (Id objednávky + přístupový klíč); podrobnosti si příjemce
  přečte přes `IOrderDirectory` — v outboxu se neukládají osobní údaje navíc.
- **Notifications** je idempotentní: e-mail daného druhu z jedné události vznikne nejvýš
  jednou (UNIQUE `source_event_id` + `kind`). E-mail jde nejdřív do fronty, odesílá se
  zvlášť (MailKit, SMTP Wedos 587 STARTTLS) s opakováním; po 24 h neodeslaný propadne.
- **Odesílatel** `Wake Park Džbán <noreply@wakeparksro.cz>`, odpovědi na `info@wake-dzban.cz`.
  Odesílání se na serveru zapne (`Notifications:Enabled`), až budou na doméně SPF, DKIM a DMARC.
- Lokálně Mailpit (`deploy/docker-compose.dev.yml`), v testech Mailpit v Testcontainers.

## Vouchery a daňový doklad (22. 10. 2026)

- **Daňový doklad** vzniká při spárování platby **ve stejné transakci** jako zaplacení
  objednávky. Číslo `FV_E_{rok}-{pořadí:000000}` (E = e-shop, odliší řadu od faktur z iDokladu) z čítače, který se zamyká uvnitř transakce —
  při chybě se vrátí i číslo, řada je **bez mezer**. DUZP = den zaúčtování platby, doklad je
  neměnný (prodávající, kupující i řádky zamrazené). Údaje prodávajícího zatím z konfigurace.
- **Modul Vouchers** konzumuje `OrderPaid` z outboxu Billingu a vystaví **jeden poukaz na
  kus** (kód `WD-XXXX-XXXX` z 31 nezaměnitelných znaků), platnost z konfigurace
  (1. 5. – 30. 9. 2027). Vystavení i událost `VouchersIssued` jdou do vlastního outboxu
  v jedné transakci; idempotence přes UNIQUE (objednávka, produkt, kus).
- **Notifications** na `VouchersIssued` pošle e-mail s PDF poukazů a dokladu.
- **PDF: QuestPDF** pod community licencí (Wake Park má obrat pod 1 mil. USD). Při prodeji
  WakeParku dalším parkům by licenci řešil každý zvlášť — knihovna je proto za rozhraním
  `IDocumentRenderer`. Vzhled je provizorní, finální navrhne grafik.
- Poznámku o jednoúčelovém poukazu na dokladu (přesné znění, případně paragraf) potvrdí účetní.

## Otevřené otázky

1. ~~Potvrdit s účetní režim jednoúčelového poukazu~~ — potvrzeno 21. 10. 2026, 12 %.
2. Až s backoffice: co produkty znamenají na místě (sloty, počet jízd permanentky, na jméno,
   nebo přenosná).
3. Definitivní formát čísla objednávky a daňového dokladu.
