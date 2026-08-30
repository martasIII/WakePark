# 0004 — Práce s časem a penězi

**Stav:** Přijato · 30. 8. 2026

## Kontext

Rezervační systém stojí a padá s časem, billing s penězi. Obojí je oblast, kde se chyby
neprojeví hned, ale při přechodu na letní čas nebo při první roční uzávěrce.

## Rozhodnutí

### Čas

- **Okamžik** (kdy se něco stalo, kdy začíná slot) — vždy UTC, `DateTimeOffset`, sloupec
  `timestamptz`, název končí na `Utc`.
- **Denní doba** (provozní doba) — lokální čas parku, typ `time`. Bez data, bez zóny.
- Park má **IANA identifikátor zóny** (`Europe/Prague`). Bez něj nelze provozní dobu převést
  na okamžik přes přechod na letní čas.
- Intervaly jsou **polouzavřené `[start, end)`**. Konec je vždy exkluzivní — jinak vzniknou
  překryvy na hranicích slotů.
- Aplikační kód nikdy nečte `DateTimeOffset.UtcNow` přímo, jen přes `IClock`. Jinak nejde
  otestovat nic, co závisí na čase.
- `InvariantGlobalization` je **vypnuté**. Bez ICU by `TimeZoneInfo` neuměl IANA identifikátory.

### Peníze

- `decimal(18,2)` + ISO 4217 kód měny. Nikdy `float` ani `double`.
- Součet částek v různých měnách je výjimka, ne tichá konverze (`Money.Add`).
- **Řádek objednávky si cenu zamrazí.** `OrderLine` obsahuje `UnitPrice` a `VatRate`
  zkopírované z ceníku v okamžik prodeje, ne cizí klíč na `PriceItem`. Zdražení příští sezónu
  nesmí změnit loňské doklady.
- **Finanční doklady jsou neměnné.** Oprava = nový doklad (dobropis), nikdy UPDATE ani DELETE.
- **Zůstatek kreditu není sloupec.** Počítá se jako suma append-only pohybů
  (`CreditTransaction`). Oprava = nová opačná transakce.

## Důsledky

Zůstatek permanentky vyžaduje agregaci místo čtení jednoho sloupce. Až to začne být pomalé,
řeší se to materializovaným pohledem nebo snapshotem — ne tím, že se zavede přepisovaný
sloupec. Auditovatelnost je u peněz důležitější než pár ušetřených milisekund.
