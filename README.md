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
| `Identity` | Kdo je to a co smí? | `UserAccount`, `Role`, `Permission`, `UserParkRole` |

## Stack

| Vrstva | Volba | Proč |
|---|---|---|
| Backend | .NET 10 (LTS) | podpora do listopadu 2028; .NET 8 končí 10. 11. 2026 |
| Frontend | Angular 22, zoneless, SCSS | Node 22.23.2, TypeScript 6 |
| Databáze | PostgreSQL 17 | zdarma, bez licenčních pastí, běží kdekoli |
| ORM | EF Core 10, code-first | migrace verzované v repu |
| Architektura | modulární monolit + YARP gateway | viz [ADR 0001](docs/adr/0001-modularni-monolit.md) |
| API | REST/JSON, OpenAPI, Swagger UI + Scalar | kontrakt generuje klienta pro frontend |
| Testy | xunit.v3, Shouldly, NetArchTest, Testcontainers | architektonická pravidla se vynucují buildem |

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
  WakePark.ArchitectureTests/        hlídá směr závislostí a izolaci modulů
  Modules/<Modul>/...UnitTests/      doména a případy užití, bez databáze
  Modules/<Modul>/...IntegrationTests/  proti reálnému Postgresu v Testcontainers
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
- PostgreSQL 17 (lokálně nebo v Dockeru), Docker pro integrační testy
- **Visual Studio 2026** nebo Rider. VS 2022 řešení **neotevře** — .NET SDK 10 vyžaduje
  MSBuild 18, poslední řada VS 2022 (17.14) nese MSBuild 17. Z příkazové řádky
  a z VS Code se staví bez problémů. Viz [ADR 0003](docs/adr/0003-dotnet-10.md).

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

Design-time factory každého modulu míří na tuhle lokální databázi, takže `dotnet ef` funguje
bez další konfigurace. Pro jiný cíl nastav `WAKEPARK_DESIGNTIME_CONNECTION`.

Nová migrace:

```bash
dotnet dotnet-ef migrations add NazevMigrace --project src/backend/Modules/Reservations/WakePark.Modules.Reservations.Infrastructure --context ReservationsDbContext --output-dir Persistence/Migrations
```

### Spuštění

```bash
dotnet run --project src/backend/WakePark.Api
```

API poslouchá na **dvou portech s různým schématem** — jak to generuje šablona ASP.NET Core:

| Port | Schéma |
|---|---|
| `7101` | **https** |
| `5101` | **http** |

Zaměnit je znamená `ERR_SSL_PROTOCOL_ERROR`: `https://localhost:5101` je HTTPS požadavek
na HTTP port. Ve Visual Studiu vyber profil *WakePark.Api (https)*, prohlížeč se otevře sám.

Gateway (`https://localhost:7100`) se pouští zvlášť a přeposílá `/api/**` na API.

#### Dokumentace API

Jeden OpenAPI dokument, dvě čtečky — vyber si:

| Adresa | Co to je |
|---|---|
| `/swagger` | Swagger UI. Umí *Try it out*, takže se endpointy dají rovnou zkoušet. |
| `/scalar/v1` | Scalar. Čitelnější, generuje ukázky volání v různých jazycích. |
| `/openapi/v1.json` | Samotný dokument. Z něj se generuje klient pro Angular. |
| `/health` | Kontrola, že aplikace i databáze žijí. |

Publikuje se **jen ve vývojovém prostředí**. Popisy endpointů včetně dokumentovaných chybových
odpovědí jsou v metadatech u `Map*Endpoints` v každém modulu.

| Metoda | Endpoint | Co dělá |
|---|---|---|
| `POST` | `/api/v1/identity/login` | přihlášení, vystaví cookie |
| `POST` | `/api/v1/identity/logout` | odhlášení |
| `GET` | `/api/v1/identity/me` | kdo je přihlášený a co smí |
| `GET` | `/api/v1/tenancy/parks` | všechny areály |
| `GET` | `/api/v1/tenancy/enterprises/{id}/parks` | areály provozovatele |
| `GET` | `/api/v1/operations/cables/{id}/schedule?date=` | rozvrh vleku pro den |
| `GET` | `/api/v1/reservations/availability?cableId=&date=` | volná kapacita |
| `POST` | `/api/v1/reservations` | vytvoří rezervaci |
| `POST` | `/api/v1/reservations/{id}/cancel` | stornuje podle politiky areálu |

#### Přihlášení

Ve vývoji se při startu API založí správce podle `appsettings.Development.json`. **Heslo tam
není** — nastav si ho mimo repo:

```bash
dotnet user-secrets set "Identity:BootstrapAdmin:Password" "zvol-si-heslo" --project src/backend/WakePark.Api
```

Bez hesla se správce nezaloží a v logu je o tom řádek. Seeder taky nepokračuje, pokud nejsou
aplikované migrace — databázi si sám měnit nebude.

### Frontend

```bash
cd src/frontend/wakepark-web
npm start
```

Běží na `http://localhost:4200`. Požadavky na `/api/**` proxuje `proxy.conf.json` na gateway
(`http://localhost:5100`), takže se ve vývoji neřeší CORS a frontend volá stejné cesty jako
v produkci.

Aby stránka ukázala data, musí běžet **všechno tři**: databáze, `WakePark.Api` (5101)
a `WakePark.Gateway` (5100). Když něco z toho chybí, přehled to řekne místo prázdné stránky.

Stránky se načítají líně (`loadComponent`), stav dotazu drží `httpResource` — načítání i chyba
jsou signály, takže se v komponentách nedrží žádné ruční `isLoading`.

Vyžaduje Node `^22.22.3` nebo `^24.15.0`.

#### Vývoj frontendu ve VS Code

Otevři **kořen repa**, ne jen složku s Angularem — jinak nebudou sedět cesty v úlohách.
VS Code sám nabídne doporučená rozšíření z `.vscode/extensions.json`.

- **F5** spustí `ng serve` a otevře Edge s připojeným debuggerem. Body zastavení fungují
  přímo v `.ts` souborech.
- `Ctrl+Shift+P` → *Tasks: Run Task* nabídne jednotlivé části i úlohu
  **vse: db + api + gateway + fe**, která nahodí celé prostředí najednou.

Backend se dá ladit ve VS Code taky (C# Dev Kit), ale pohodlnější je na něj Visual Studio.
Ať už si vybereš cokoli, **nepouštěj API z obou najednou** — druhé spuštění selže na
zamčených souborech.

#### Zobrazení na telefonu

```bash
npx ng serve --host 0.0.0.0
```

Pak `http://<IP-počítače>:4200` ze zařízení ve stejné síti. Windows Firewall si při prvním
spuštění řekne o povolení.

### Testy

```bash
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
```

Integrační testy si zvednou vlastní PostgreSQL kontejner (Testcontainers), takže **vyžadují
běžící Docker**. Bez něj:

```bash
powershell -ExecutionPolicy Bypass -File scripts/test.ps1 -SkipIntegration
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
- Primární klíče přiřazuje **aplikace** (UUID v7), ne databáze. V EF je to explicitně nastavené
  (`ValueGenerated.Never`) — jinak by EF u nového objektu v agregátu, který klíč už má,
  vydal UPDATE místo INSERT.
- Enumy jdou po drátě jako **text**. Čísla by znamenala, že vložení hodnoty doprostřed výčtu
  tiše změní význam uložených dat.
- Session drží **HttpOnly cookie**, ne token v prohlížeči. Do JavaScriptu se nedostane nic,
  co by šlo ukrást přes XSS.
- Oprávnění platí **v konkrétním areálu**, ne globálně. Otázka „je admin?" nemá bez areálu smysl.
- Oprávnění se čtou z databáze **při každém požadavku**, ne z cookie. Odebrání role se tak
  projeví hned, ne až po odhlášení.
- Neúspěšné přihlášení odpovídá **vždy stejně**, ať účet neexistuje nebo je špatné heslo.
  Jinak by šlo zkoušením zjistit, které e-maily v systému jsou.
- Kontroly oprávnění na frontendu jen **skrývají tlačítka**. Rozhoduje backend.

Podrobně v [docs/datamodel.md](docs/datamodel.md).
