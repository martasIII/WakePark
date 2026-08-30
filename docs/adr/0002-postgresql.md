# 0002 — PostgreSQL a míra nezávislosti na DB enginu

**Stav:** Přijato · 30. 8. 2026

## Kontext

Požadavek: databáze zdarma a **nezávislost na konkrétním DB enginu**.

## Rozhodnutí

**PostgreSQL 17** jako jediná podporovaná databáze, přístup přes EF Core 10 + Npgsql.

Nezávislost chápeme jako **schopnost vyměnit engine za jednotky dnů práce**, ne za změnu
jednoho řádku konfigurace. Absolutní nezávislost by stála výkon i čas a v praxi se stejně
nepoužije. Drží ji tato pravidla:

- Doména a aplikační vrstva neznají EF Core ani Npgsql. Hlídá to architektonický test.
- Žádné raw SQL mimo `Infrastructure`.
- Žádné typy providera v entitách (`NpgsqlRange`, `jsonb`, pole).
- Peníze `decimal(18,2)` + ISO 4217 kód měny.
- Výpočty v C#, ne v databázových funkcích nebo triggerech.
- Klíče `Guid` (UUID v7), ne engine-specifické sekvence.

Specifika Postgresu (`timestamptz`, ICU kolace, `xmin` jako `RowVersion`) používáme vědomě
a soustředěně na jednom místě — `NpgsqlOptionsExtensions` a konfigurace entit.

## Důsledky

Sloupce jsou `snake_case` (`UseSnakeCaseNamingConvention`), protože Postgres nekvótované
identifikátory převádí na malá písmena a PascalCase by znamenal uvozovky v každém ručním dotazu.

Vývojová databáze běží v Dockeru (`deploy/docker-compose.dev.yml`). Integrační testy poběží
proti reálnému Postgresu přes Testcontainers, ne proti in-memory provideru — ten se chová jinak
a testy by lhaly.

## Zvažované alternativy

**SQL Server.** Developer edice je zdarma jen pro vývoj, produkce znamená licenci.
**MariaDB / MySQL.** Slabší práce s časovými zónami, transakčním DDL a CTE.
**SQLite.** Použitelné pro Dochazku, ne pro billing — slabá typovost, `decimal` jako `REAL`,
prakticky žádná souběžnost zápisů.
