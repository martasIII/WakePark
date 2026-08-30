# Datový model

Stav: **implementováno pro moduly Tenancy, Operations a Reservations** (verze 0.4). Vychází z ručního náčrtu a připomínek z 30. 8. 2026.
Entity a atributy jsou anglicky, komentáře česky.

---

## Globální konvence

Platí pro **všechny** entity, nebudou se opakovat u každé tabulky.

| Konvence | Pravidlo |
|---|---|
| Primární klíč | `Guid` (UUID v7 – sekvenční, šetrný k indexům). Sloupec `Id`. |
| Audit | `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy` – doplňuje EF interceptor, ne aplikační kód. |
| Souběh | `RowVersion` (`xmin`) u entit, kde hrozí kolize: `Slot`, `RidePass`, `Order`. |
| Mazání | Soft delete (`DeletedAtUtc`, `DeletedBy`) tam, kde je potřeba historie. Finanční doklady se **nemažou nikdy**. |
| Čas – okamžik | Vždy UTC, sloupec končí na `Utc`, typ `timestamptz`. |
| Čas – denní doba | Lokální čas parku, typ `time`. Bez zóny, bez data. |
| Časový interval | Polouzavřený `[start, end)`. Konec je vždy exkluzivní. |
| Peníze | `decimal(18,2)` + `Currency` (ISO 4217, 3 znaky). Nikdy `float`/`double`. |
| Tenant | `EnterpriseId` na každé tenant-scoped entitě + globální EF query filter. |
| Číselníky | Pevné stavy = C# `enum` uložený jako `string`. Uživatelsky spravované = tabulka. |
| Pojmenování | Entity jednotné číslo, PascalCase. Cizí klíč `<Entita>Id`. |

---

## Modul `Tenancy`

Kdo systém provozuje a jaké areály má.

### `Enterprise` — provozovatel (tenant)

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK, zároveň **TenantId** pro celý systém |
| `Name` | string(200) | |
| `Address` | *value object* | vloženo přes `OwnsOne`, ne samostatná tabulka |
| `RegistrationNumber` | string(20) | IČO |
| `VatNumber` | string(20)? | DIČ, null když neplátce |
| `BankAccountNumber` | string(50)? | tuzemský formát |
| `Iban` | string(34)? | |
| `OwnerUserAccountId` | Guid | FK → `Identity.UserAccount` (cross-modul: **jen ID, ne navigace**) |

### `WakePark` — areál

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `EnterpriseId` | Guid | FK → `Enterprise` |
| `Name` | string(200) | |
| `Description` | string(2000)? | |
| `Address` | *value object* | |
| `TimeZoneId` | string(64) | IANA, např. `Europe/Prague` — **nutné** pro převod lokální doby na UTC |
| `Currency` | string(3) | ISO 4217, výchozí měna pro doklady |
| `ReservationEmail` | string(320)? | adresa pro notifikace o rezervacích |
| `CancellationPolicy` | *value object* | pravidla storna, viz níže |

Pozn.: `pricelistId` z náčrtu **zrušen** — vazba jde opačně, `PriceList` má `WakeParkId` + platnost od/do.

### `CancellationPolicy` — value object

Storno je konfigurovatelné **na úrovni areálu**, ne globálně — každý park má jiná pravidla.

| Složka | Typ | Význam |
|---|---|---|
| `FreeCancellationHours` | int | kolik hodin před startem slotu lze stornovat bez sankce |
| `LateCancellationRefundPercent` | decimal(5,2) | kolik % se vrací při pozdním stornu (0 = nic, 100 = vše) |
| `NoShowRefundPercent` | decimal(5,2) | kolik % se vrací, když zákazník nepřijde |
| `CancellationAllowed` | bool | false = storno není možné vůbec |

Vrácení se počítá z toho, čím bylo placeno: kredit se vrací jako `CreditTransaction`
s kladnou `Delta`, peníze jako dobropis. Nikdy se needituje původní doklad.

### `Address` — value object (ne tabulka)

`Street`, `HouseNumber`, `City`, `PostalCode`, `CountryCode` (ISO 3166-1 alpha-2)

---

## Modul `Identity`

Přihlášení a oprávnění. **Nesmí obsahovat osobní údaje** — ty jsou v `Customers`/`Staff`.

### `UserAccount`

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `Email` | string(320) | UNIQUE, přihlašovací identifikátor |
| `PasswordHash` | string(500) | ASP.NET Core Identity hasher. **Nikdy plain heslo.** |
| `SecurityStamp` | string | invalidace tokenů při změně hesla |
| `Status` | enum | `Pending`, `Active`, `Suspended`, `Locked` |
| `EmailConfirmedAtUtc` | timestamptz? | |
| `LastLoginAtUtc` | timestamptz? | |

### `Role` — logická role

`Id`, `EnterpriseId?`, `Name`, `Description`

Systémové role mají `EnterpriseId = null` (Guest, Customer, Employee, Instructor, Admin), tenant si může přidat vlastní.

### `Permission` — konkrétní oprávnění

`Id`, `Code` (UNIQUE, např. `reservation.delete`), `Description`

V náčrtu `PhysicalRole` s `parentRoleId`. Přejmenováno a hierarchie nahrazena M:N — jinak nelze dát stejné oprávnění dvěma rolím bez duplikace. `Code` je zároveň konstanta v C# (`Permissions.Reservation.Delete`), tabulka slouží jen pro administraci.

### `RolePermission`

`RoleId`, `PermissionId` — složený PK

### `UserParkRole` — kdo má jakou roli v jakém areálu

`Id`, `UserAccountId`, `WakeParkId`, `RoleId`, `ValidFromUtc`, `ValidToUtc?`

UNIQUE `(UserAccountId, WakeParkId, RoleId)`

Nahrazuje `User.roleId` + `User.wakeParkId` z náčrtu. Jeden člověk může mít roli ve víc areálech a víc rolí v jednom.

---

## Modul `Customers`

### `Customer`

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `EnterpriseId` | Guid | |
| `UserAccountId` | Guid? | **null = walk-in host bez registrace** |
| `FirstName`, `LastName` | string(100) | |
| `Email` | string(320)? | |
| `Phone` | string(30)? | |
| `DateOfBirth` | date? | kvůli juniorské sazbě a nezletilosti |
| `Address` | *value object*? | |
| `Note` | string(1000)? | |

### `CustomerCard` — RFID čip / karta

`Id`, `CustomerId`, `CardNumber` (UNIQUE), `IssuedAtUtc`, `RevokedAtUtc?`

### `Consent` — souhlasy (GDPR, zákonný zástupce)

`Id`, `CustomerId`, `Type` (enum: `GdprProcessing`, `Marketing`, `GuardianApproval`, `LiabilityWaiver`), `GrantedAtUtc`, `RevokedAtUtc?`, `DocumentUrl?`, `GuardianName?`

---

## Modul `Operations`

Provozní realita areálu: co jezdí, kdy a s jakou kapacitou.

### `Cable` — vlek

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `WakeParkId` | Guid | |
| `Name`, `Description` | | |
| `TowerCount` | int | 2 pro dvousloup |
| `IsActive` | bool | |

`slotsPerHour` z náčrtu **rozděleno** na dvě veličiny a přesunuto na `WorkingHours` — délka jízdy i kapacita se mění podle sezóny a denní doby.

**Provozní realita dvousloupu:** na laně je v jednu chvíli **vždy jen jeden jezdec**. Víc lidí
ve slotu znamená, že se v něm **střídají**, ne že jedou současně. Kapacita slotu je tedy počet
jezdců sdílejících blok, ne počet lan.

### `Season` — sezóna

`Id`, `WakeParkId`, `CableId?` (null = platí pro všechny vleky), `Name`, `StartDate`, `EndDate`

### `WorkingHours` — provozní doba

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `CableId` | Guid | |
| `SeasonId` | Guid | |
| `DayOfWeek` | int (0–6) | nahrazuje `weekend` bool z náčrtu |
| `StartTime`, `EndTime` | time | lokální čas parku |
| `SlotDurationMinutes` | int | délka bloku (10 / 15 / 20 / 30 / 60) |
| `Capacity` | int | kolik jezdců si blok sdílí a **střídá se** v něm (u nás 3, jinde jinak) |

### `ScheduleOverride` — výjimka z provozní doby

`Id`, `CableId`, `Date`, `IsClosed`, `CustomStartTime?`, `CustomEndTime?`, `Reason`

V náčrtu i v první verzi tohoto dokumentu `OperatingException`. Přejmenováno: typ končící
na `Exception`, který není výjimkou, mate čtenáře i analyzátory.

Bouřka, údržba, závody, svátek. U wakeparku běžná věc, ne okrajový případ.

## Modul `Reservations`

Slot je záměrně tady, ne v `Operations` — obsazenost a odečet kreditu musí proběhnout
v jedné transakci. Zdůvodnění v [ADR 0005](adr/0005-kde-zije-slot.md).

### `Slot` — kapacitní jednotka v čase

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `CableId` | Guid | |
| `StartUtc` | timestamptz | UNIQUE spolu s `CableId` |
| `DurationMinutes` | int | zamrazeno z `WorkingHours` v okamžiku vzniku |
| `Capacity` | int | zamrazeno z `WorkingHours`; počet jezdců, kteří se ve slotu vystřídají |
| `ReservedCount` | int | počet obsazených míst, `<= Capacity` |
| `Status` | enum | `Open`, `Closed`, `Cancelled` |
| `RowVersion` | xmin | optimistic concurrency |

**Materializace:** řádek vzniká **lazy** — až s první rezervací v daném čase. Kalendář se dopředu negeneruje.

**Invariant:** `ReservedCount <= Capacity`. Překročení = HTTP 422, kolize souběhu = HTTP 409.

Kapacita se **zamrazuje do slotu** při jeho vzniku. Změna `WorkingHours` tak nikdy zpětně
nezneplatní už přijaté rezervace.

---


### `Reservation`

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `SlotId` | Guid | |
| `CustomerId` | Guid | |
| `OrderId` | Guid? | null dokud není vystaven doklad |
| `Seats` | int | kolik míst ve slotu zabírá (obvykle 1) |
| `Status` | enum | `Pending`, `Confirmed`, `CheckedIn`, `Completed`, `Cancelled`, `NoShow` |
| `Source` | enum | `Online`, `Desk`, `Phone` |
| `IdempotencyKey` | string(64)? | UNIQUE — ochrana proti dvojímu odeslání |
| `CancelledAtUtc`, `CancellationReason` | | |
| `RowVersion` | xmin | |

### `RidePass` — permanentka / kredit

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `CustomerId` | Guid | |
| `WakeParkId` | Guid | |
| `Kind` | enum | `RideCredit` (počet jízd), `SeasonPass` (neomezeně v období) |
| `ValidFrom`, `ValidTo` | date | |
| `IsTransferable` | bool | |
| `Status` | enum | `Active`, `Expired`, `Suspended`, `Refunded` |
| `RowVersion` | xmin | |

**Zůstatek NENÍ sloupec.** Počítá se jako suma `CreditTransaction.Delta`.

### `CreditTransaction` — pohyb kreditu (append-only)

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `RidePassId` | Guid | |
| `Delta` | int | v **jízdách**, kladné = nabití, záporné = čerpání |
| `Reason` | enum | `Purchase`, `RideConsumed`, `Refund`, `ManualAdjustment`, `Expiry` |
| `ReservationId` | Guid? | |
| `OccurredAtUtc` | timestamptz | |
| `Note` | string(500)? | |

Řádky se **nikdy neaktualizují ani nemažou**. Oprava = nová opačná transakce.

---

## Modul `Catalog`

### `PriceList`

`Id`, `WakeParkId`, `Name`, `ValidFrom`, `ValidTo?`, `IsActive`

### `PriceItem`

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `PriceListId` | Guid | |
| `ProductId` | Guid | |
| `UnitPrice` | decimal(18,2) | |
| `Currency` | string(3) | |
| `VatRate` | decimal(5,2) | sazba v procentech |
| `CustomerSegment` | enum? | `Standard`, `Junior`, `Student`, `Senior` — null = pro všechny |
| `DayType` | enum? | `Weekday`, `Weekend`, `Holiday` — null = pro všechny |

### `Product` — co se prodává

`Id`, `EnterpriseId`, `Code` (UNIQUE), `Name`, `Type` (enum: `SingleRide`, `RideCreditPack`, `SeasonPass`, `Other`), `RideCount?`

---

## Modul `Billing`

Finanční doklady jsou **immutable**. Oprava = nový doklad (dobropis), nikdy UPDATE.

### `Order`

`Id`, `EnterpriseId`, `WakeParkId`, `CustomerId`, `Number` (UNIQUE, číselná řada), `Status` (enum: `Draft`, `AwaitingPayment`, `Paid`, `Cancelled`, `Refunded`), `Currency`, `TotalNet`, `TotalVat`, `TotalGross`, `IssuedAtUtc`, `RowVersion`

### `OrderLine`

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `OrderId` | Guid | |
| `ProductId` | Guid | jen reference, ne zdroj ceny |
| `Description` | string(500) | **zkopírováno** z produktu |
| `Quantity` | decimal(18,3) | |
| `UnitPrice` | decimal(18,2) | **zamrazená cena** z ceníku |
| `VatRate` | decimal(5,2) | **zamrazená sazba** |
| `LineNet`, `LineVat`, `LineGross` | decimal(18,2) | |
| `ReservationId` | Guid? | vazba na to, co se prodalo |

Klíčové pravidlo: řádek **nikdy** neodkazuje na `PriceItem` kvůli ceně. Když příští sezónu zdražíš, loňské doklady se nesmí změnit.

### `Payment`

`Id`, `OrderId`, `Method` (enum: `Cash`, `Card`, `BankTransfer`, `OnlineGateway`, `RidePass`), `Amount`, `Currency`, `Status` (enum: `Pending`, `Succeeded`, `Failed`, `Refunded`), `ExternalTransactionId?`, `IdempotencyKey` (UNIQUE), `ReceivedAtUtc`

---

## Modul `Staff`

Přebírá agendu stávajícího systému Dochazka.

### `Employee`

`Id`, `EnterpriseId`, `WakeParkId`, `UserAccountId?`, `FirstName`, `LastName`, `Phone`, `Address` (VO), `Position`, `HourlyRate`, `Currency`, `EmploymentType` (enum: `Brigade`, `Contract`, `Employment`), `StartDate`, `EndDate?`

### `Shift` — plánovaná směna

`Id`, `EmployeeId`, `CableId?`, `PlannedStartUtc`, `PlannedEndUtc`, `Status` (enum: `Planned`, `Confirmed`, `Cancelled`)

### `WorkHour` — odpracovaný čas

`Id`, `EmployeeId`, `ShiftId?`, `StartUtc`, `EndUtc?`, `Source` (enum: `Terminal`, `Manual`, `Import`), `ApprovedBy?`, `ApprovedAtUtc?`

---

## Modul `Notifications`

### `Message`

| Sloupec | Typ | Pozn. |
|---|---|---|
| `Id` | Guid | PK |
| `RecipientUserAccountId` | Guid? | |
| `RecipientAddress` | string(320) | e-mail nebo telefon — doklad, kam to reálně šlo |
| `Channel` | enum | `Email`, `Sms`, `InApp` |
| `Type` | enum | `ReservationConfirmed`, `ReservationCancelled`, `PaymentReceipt`, `PassExpiring`, `Custom` |
| `Subject`, `Body` | | |
| `Status` | enum | `Queued`, `Sent`, `Failed`, `Cancelled` |
| `SentAtUtc`, `FailedAtUtc`, `FailureReason`, `RetryCount` | | |

---

## Vědomě odložené (mimo MVP)

| Oblast | Poznámka |
|---|---|
| Půjčovna vybavení | Prkna, vesty, helmy, neopreny — velikosti, kusová evidence, servis |
| Tréninky a kurzy | Instruktor, kapacita skupiny, opakované lekce |
| Závody a akce | Registrace, startovní listiny, výsledky |
| Věrnostní program | Body, slevy za objem |
| Účetní export | ISDOC, napojení na účetní SW |

Struktura je na ně připravená (`Product.Type`, `OrderLine`, `Reservation`), rozšíření nebude vyžadovat přepis jádra.

---

## Vyřešené otázky

| Otázka | Rozhodnutí |
|---|---|
| Prodává se čas, nebo jízdy? | **Jízdy.** Kredit permanentky se vede v jízdách, ne v minutách. |
| Kolik lidí najednou na laně? | **Jeden.** Víc lidí ve slotu se střídá. |
| Tenant | `Enterprise` |
| Storno | Konfigurovatelné **na úrovni areálu** (`Park.CancellationPolicy`) |
| Docházka brigádníků | Stěhuje se sem jako modul `Staff` |
| Platební brána | Odloženo mimo MVP |

## Otevřené otázky

1. Číselné řady dokladů — jedna na `Enterprise`, nebo na `WakePark`? Reset po roce?
2. Může jeden zákazník rezervovat víc míst v jednom slotu (rodina na jeden doklad),
   nebo je vždy jedna rezervace = jeden jezdec?
3. Stojí „jedna jízda" v kreditu vždy 1 bez ohledu na délku bloku, nebo se odečítá podle
   `SlotDurationMinutes`?
4. EET / evidence tržeb — aktuálně zrušena, ale hotovostní uzávěrka směny bude potřeba.
   Řešit v MVP?
