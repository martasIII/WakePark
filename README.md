# WakePark

Rezervační, billingový a informační systém pro wakeboardingové parky, primárně dvousloupé.

Stav: **jádro rezervací hotové**. Stojí architektura, sdílené stavební bloky a tři moduly:
`Tenancy` (provozovatel a areály), `Operations` (vleky a rozvrh) a `Reservations` (sloty,
rezervace, permanentky). Zbytek zatím existuje jen jako popsaný datový model.

| Modul | Odpovídá na otázku | Vlastní |
|---|---|---|
| `Tenancy` | Kdo systém provozuje a kde? | `Enterprise`, `Park` (vč. politiky storna) |
| `Operations` | Kdy by se dalo jezdit? | `Cable`, `Season`, `WorkingHours`, `ScheduleOverride` |
| `Reservations` | Co je volné a kdo to má? | `Slot`, `Reservation`, `RidePass`, `CreditTransaction` |

## Stack

| Vrstva | Volba | Proč |
|---|---|---|
| Backend | .NET 10 (LTS) | podpora do listopadu 2028; .NET 8 končí 10. 11. 2026 |
| Frontend | Angular 22, zoneless, SCSS | Node 22.23.2, TypeScript 6 |
| Databáze | PostgreSQL 17 | zdarma, bez licenčních pastí, běží kdekoli |
| ORM | EF Core 10, code-first | migrace verzované v repu |
| Architektura | modulární monolit + YARP gateway | viz [ADR 0001](docs/adr/0001-modularni-monolit.md) |
| API | REST/JSON, OpenAPI, Scalar | kontrakt generuje klienta pro frontend |
| Testy | xunit.v3, Shouldly, NetArchTest | architektonická pravidla se vynucují buildem |

## Struktura

```
src/backend/
  BuildingBlocks/          sdílený základ, žádná byznys logika
    ...Domain/             Entity, AggregateRoot, ValueObject, Money, Address
    ...Application/        IClock, ICurrentUser, ITenantContext, ICommand/IQuery
    ...Infrastructure/     audit interceptor, konvence EF, Npgsql nastavení
  Modules/<Modul>/
    ...Contracts/          JEDINÁ assembly viditelná pro ostatní moduly (DTO, události)
    ...Domain/             entity a doménová pravidla
    ...Application/        případy užití, rozhraní repozitářů
    ...Infrastructure/     EF konfigurace, repozitáře, HTTP endpointy, DI
  WakePark.Api/            host, který moduly skládá dohromady
  WakePark.Gateway/        YARP – jediný vstupní bod pro frontend
src/frontend/
  wakepark-web/            Angular aplikace
tests/
  WakePark.ArchitectureTests/   hlídá směr závislostí a izolaci modulů
  Modules/<Modul>/...UnitTests/
docs/
  datamodel.md             specifikace datového modelu
  adr/                     architektonická rozhodnutí a jejich důvody
deploy/                    docker-compose pro lokální databázi
scripts/                   pomocné skripty
```

Pravidlo, které drží celý návrh pohromadě: **modul smí odkazovat jen na `*.Contracts` jiného
modulu, nikdy na jeho `Domain`, `Application` nebo `Infrastructure`.** Vynucují to testy
v `WakePark.ArchitectureTests`, takže porušení shodí build, ne až produkci.

## Rozjetí

### Předpoklady

- .NET SDK 10.0.400+
- PostgreSQL 17 (lokálně nebo v Dockeru)
- Visual Studio 2022 (17.12+) nebo Rider

### Databáze

```bash
docker compose -f deploy/docker-compose.dev.yml up -d
```

Běží na `localhost:5432`, databáze/uživatel/heslo `wakepark`. Vývojové heslo je v repu
záměrně — je to lokální kontejner bez dat. Produkční connection string se nikdy necommituje.

### Migrace

```bash
dotnet tool restore
dotnet dotnet-ef database update --project src/backend/Modules/Tenancy/WakePark.Modules.Tenancy.Infrastructure --context TenancyDbContext
```

Každý modul má **vlastní schéma a vlastní historii migrací** (`<schema>.__ef_migrations_history`),
takže se aktualizuje samostatně — `TenancyDbContext`, `OperationsDbContext`, `ReservationsDbContext`.

Nová migrace:

```bash
dotnet dotnet-ef migrations add NazevMigrace --project src/backend/Modules/Reservations/WakePark.Modules.Reservations.Infrastructure --context ReservationsDbContext --output-dir Persistence/Migrations
```

### Spuštění

```bash
dotnet run --project src/backend/WakePark.Api
```

API: `https://localhost:7101`, dokumentace `https://localhost:7101/scalar/v1`, health `/health`.
Gateway (`https://localhost:7100`) se pouští zvlášť a přeposílá `/api/**` na API.

| Metoda | Endpoint | Co dělá |
|---|---|---|
| `GET` | `/api/v1/tenancy/enterprises/{id}/parks` | areály provozovatele |
| `GET` | `/api/v1/operations/cables/{id}/schedule?date=` | rozvrh vleku pro den |
| `GET` | `/api/v1/reservations/availability?cableId=&date=` | volná kapacita |
| `POST` | `/api/v1/reservations` | vytvoří rezervaci |
| `POST` | `/api/v1/reservations/{id}/cancel` | stornuje podle politiky areálu |

### Frontend

```bash
cd src/frontend/wakepark-web
npm start
```

Běží na `http://localhost:4200`. Požadavky na `/api/**` proxuje `proxy.conf.json` na gateway
(`http://localhost:5100`), takže se ve vývoji neřeší CORS a frontend volá stejné cesty jako
v produkci.

Vyžaduje Node `^22.22.3` nebo `^24.15.0`.

### Testy

```bash
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
```

`dotnet test` v SDK 10.0.400 nefunguje s xunit.v3 — proces naběhne, ale nenajde žádný test
a skončí s kódem 5. Skript proto pouští testovací projekty přes `dotnet run`, což dává stejný
výsledek. Až se to v SDK opraví, skript zmizí. Test Explorer ve VS 2022 tím není dotčený.

## Konvence

- Build běží s `TreatWarningsAsErrors`. Varování se neodkládají.
- Sloupce v databázi jsou `snake_case`, schémata podle modulů.
- Peníze jsou `decimal` + kód měny. Nikdy `float`.
- Okamžiky jsou UTC (`timestamptz`), denní doba je lokální `time`, intervaly jsou `[start, end)`.
- Finanční doklady jsou neměnné. Oprava = nový doklad.
- Zůstatky kreditu se počítají z append-only pohybů, nedrží se ve sloupci.
- Na laně je vždy jeden jezdec. Kapacita slotu = kolik lidí se v něm vystřídá.
- Každý modul má **vlastní** `IUnitOfWork`. Sdílené rozhraní by se v jednom DI kontejneru
  přepsalo registrací posledního modulu a handler by ukládal cizí kontext.

Podrobně v [docs/datamodel.md](docs/datamodel.md).
