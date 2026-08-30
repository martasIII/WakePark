# 0005 — Slot patří do modulu Reservations, ne Operations

**Stav:** Přijato · 30. 8. 2026

## Kontext

Původní návrh datového modelu řadil `Slot` mezi provozní entity, vedle vleku, sezóny
a provozní doby. Vypadá to logicky: slot je přece provozní pojem.

Jenže vytvoření rezervace musí atomicky:

1. ověřit, že slot podle rozvrhu existuje,
2. zvýšit obsazenost slotu a nepřekročit kapacitu,
3. odečíst kredit z permanentky,
4. založit rezervaci.

Kroky 2 až 4 tvoří jeden invariant. Kdyby `Slot` žil v modulu Operations, běžely by tyto kroky
nad **dvěma různými `DbContext`y** — a tedy ve dvou transakcích. Z jednoduchého `SaveChanges`
by se stala saga s kompenzacemi, se všemi jejími chybovými stavy, kvůli jedinému čítači.

## Rozhodnutí

Hranice mezi moduly vede takto:

| Modul | Vlastní | Odpovídá na otázku |
|---|---|---|
| `Operations` | `Cable`, `Season`, `WorkingHours`, `ScheduleOverride` | *Kdy by se dalo jezdit?* |
| `Reservations` | `Slot`, `Reservation`, `RidePass`, `CreditTransaction` | *Co je volné a kdo to má?* |

Operations vystavuje přes `IOperationsSchedule` **předpisy slotů** (`SlotTemplateDto`) —
vypočtené, neuložené. Reservations si z předpisu **materializuje** vlastní `Slot`, jakmile
o něj někdo poprvé projeví zájem.

Čtení rozvrhu není transakčně kritické: je to konfigurace, která se mění zřídka a ručně.
Zápis obsazenosti kritický je — a ten zůstává celý uvnitř jednoho modulu.

## Důsledky

**Co získáváme.** Invariant „obsazeno ≤ kapacita" spolu s odečtem kreditu v jedné databázové
transakci. Žádná saga, žádné kompenzace, žádný nekonzistentní mezistav.

**Co ztrácíme.** Slot je logicky provozní pojem, ale bydlí jinde, než by čtenář datového modelu
čekal. Proto tohle ADR.

**Zamrazení hodnot.** Délka a kapacita se do slotu kopírují v okamžik vzniku. Pozdější změna
provozní doby tak nikdy zpětně nezneplatní přijaté rezervace — cenou je, že se změna rozvrhu
neprojeví na slotech, které už mají rezervaci.

**Kdy to přehodnotit.** Kdyby bylo potřeba spravovat kapacitu z provozní strany nezávisle na
rezervacích (například blokovat sloty pro závody z admin rozhraní Operations), řešilo by se to
příkazem přes kontrakt Reservations, ne přesunem entity zpět.

## Zvažované alternativy

**Sdílená transakce napříč `DbContext`y.** Technicky proveditelné (společné `DbConnection`
a `UseTransaction`), ale vytváří skrytou vazbu mezi moduly, kterou architektonické testy
nezachytí — a přesně takové vazby dělají z modulárního monolitu obyčejný monolit.

**Saga s kompenzacemi.** Správné řešení pro skutečné microservices, zbytečná složitost tam,
kde obě strany běží v jednom procesu nad jednou databází.
