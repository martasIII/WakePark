# 0009 — Provozní doba po dnech, blokace a katalog v backoffice

**Stav:** Navrženo · 24. 10. 2026

## Kontext

ADR 0008 určil jako první krok backoffice moduly Operations a Catalog — stojí na nich web,
e-shop, pokladna i rezervace. Dnes:

- otevírací doba je v `oteviraci-doba.json` webu (jedna doba na celý týden + výjimky),
- ceník je v `cenik.json` webu, produkty e-shopu v `katalog.json` API — dvě oddělené věci,
- Operations má model `Season` + `WorkingHours` (doba podle dne v týdnu) + `ScheduleOverride`,
  bez UI a bez produkčních dat,
- každý deploy spouští `import-catalog`, který katalog přepíše podle `katalog.json`.

Jak to ve Wake Parku chodí (Martin, 24. 10.):

- Sezóna je jen hlavní, v zimě zavřeno. Začátek a konec určuje počasí a stav vody.
- Provozní dobu Martin připraví **na každý den sezóny zvlášť** (podle délky denního světla
  a zkušeností) a mění ji podle potřeby, třeba den dopředu.
- Oba vleky jsou pro rezervace a provoz shodné; liší se délkou dráhy a překážkami — to je
  jen popis a vizualizace.
- Blokace jsou běžné, i na část dne, s důvodem (technická závada, firemní akce, vichřice).
- Obsluha (brigádníci) smí blokovat i vytvářet a rušit rezervace na místě.

## Rozhodnutí — Operations

### Provozní den místo provozní doby podle dne v týdnu

**`OperatingDay`** — areál, datum, otevřeno od, do (lokální čas areálu). **Den bez záznamu =
zavřeno.** Začátek a konec sezóny nejsou zvláštní nastavení: jsou to první a poslední
otevřený den.

- Platí pro **celý areál** (vleky jsou provozně shodné).
- `WorkingHours` (podle dne v týdnu) se **ruší**. `Season` zůstává jen jako pojmenování
  období pro přehledy („Sezóna 2027“), provozní dobu neurčuje. `ScheduleOverride`
  nahrazuje blokace (níže). V produkci v těch tabulkách nic není.
- **Délka bloku a kapacita** (20 min, 3 jezdci) jsou **nastavení areálu**, ne údaj u dne —
  celou sezónu stejné.

**Hromadné nastavení = import CSV** (`datum;od;do`, např. vygenerované v Sheets z délky dne).
Import ukáže **náhled změn** (nové / změněné / beze změny dny) a uloží se až po potvrzení,
celý v jedné transakci. Den v CSV bez časů = zavřeno. Jednotlivé dny jdou pak upravit ručně.

### Blokace

**`Blocking`** — od–do (okamžiky, ukládané v UTC; zadávané v lokálním čase), **vlek, nebo
celý areál**, **kategorie** (technická závada, firemní akce, počasí, jiné) a volitelná
poznámka. Celodenní uzavírka = blokace přes celou provozní dobu.

- **Obsluha** blokace zadává i ruší (mobil), admin taky.
- **Na webu** se ukáže kategorie („Firemní akce“), **poznámka nikdy** — je interní.
- **Rezervace v blokovaném čase:** systém **upozorní** („v tom čase jsou 4 rezervace“)
  a obsluha zvolí: hromadný e-mail dotčeným, nebo vyřeší ručně (obvolá). Nic se neruší
  automaticky. Navázáno až s rezervacemi; blokace musí umět vrátit dotčené rezervace.
- **SMS** jako další kanál (česká SMS brána, řádově koruna za zprávu) — později,
  s rezervacemi. Podmínka: telefon u rezervace povinný.

### Kdo co vidí

- **Veřejný web:** otevírací doba **14 dní dopředu**, rezervace (až budou) **nejvýš
  měsíc dopředu**. Obojí nastavení areálu.
- **Backoffice i aplikace obsluhy:** celá sezóna, prohlížení i změny podle role.
- Web čte provozní dobu z API místo `oteviraci-doba.json`; ten zmizí. Obecná poznámka
  („Doporučujeme si před návštěvou…“) zůstává textem webu.

## Rozhodnutí — Catalog

### Jeden katalog pro ceník, e-shop i pokladnu

Každý produkt má příznaky, **kde se ukazuje**: ceník na webu, e-shop, pokladna. (Martinovi
by stačil e-shop ano/ne; tři příznaky jsou stejně levné a pokladna je bude potřebovat —
helma za 0 Kč patří do ceníku i na pokladnu, ne do e-shopu.)

Ceník webu se vykreslí z katalogu; `cenik.json` zmizí. Poznámky sekcí („Po dobu jízdného“,
„Firemní akce — napište nám“) budou **popis kategorie**; kategorie může být i bez produktů.

### Kategorie

Strom: kategorie má volitelného rodiče. Obecný model, ale validace dovolí **nejvýš dvě
úrovně** (kategorie → podkategorie). Víc úrovní = změna jedné konstanty, ne modelu.

### Ceny

- Změna ceny platí **implicitně hned**, volitelně **od data** (ceny na 2027 se připraví
  v zimě). Model `PriceList` / `PriceItem` s platností už existuje; historie cen se nemaže.
- Prodané objednávky, doklady a vouchery mají cenu zamrazenou — změna ceníku se jich
  nikdy nedotkne (platí už dnes).
- **Ceny podle typu zákazníka nebo dne** (junior, víkend) **zatím ne**. Sloupce
  `CustomerSegment` / `DayType` se nepoužijí.
- Produkt, který se už prodal, se **nemaže, jen vyřadí** (neaktivní) — objednávky na něj
  odkazují.

### Obrázky

Obrázky připraví Dan, do repa je nahraje Martin nebo Claude (`wwwroot/img/katalog/`).
Produkt odkazuje na soubor jménem; oprava obrázku = přepsat soubor stejným jménem.
**Nahrávání přes backoffice ne.** Případně později: admin vybírá ze souborů, které v repu jsou.

### Import katalogu při deployi

**Deploy přestane katalog přepisovat.** `import-catalog` naplní katalog jen **prázdný**
(první spuštění, testy, vývoj). Jakmile ho spravuje backoffice, je zdrojem pravdy databáze
a `katalog.json` je jen výchozí data. Jinak by každý deploy tiše přepsal úpravy admina.

## Postup

1. **Catalog backend:** kategorie se stromem, příznaky zobrazení, ceny od data, API pro
   admina, import jen do prázdného katalogu.
2. **Web z databáze:** ceník z katalogu místo `cenik.json`.
3. **Operations backend:** `OperatingDay`, nastavení areálu, import CSV s náhledem,
   `Blocking`, API pro admina a obsluhu; web otevírací doba z API.
4. **Angular backoffice:** přihlášení a role (Identity už je), admin obrazovky katalogu,
   provozních dnů a blokací; blokace i na mobilu pro obsluhu.

## Důsledky

**Co získáváme.** Jedna pravda o cenách i provozu pro web, e-shop a pokladnu. Provozní doba
odpovídá skutečnosti (každý den jinak, podle světla) a připraví se jedním souborem.

**Co ztrácíme.** Pravidelnou dobu „v pondělí 10–20“ už systém sám nedopočítá — každý den
se musí zadat (importem). To je záměr: odpovídá to provozu Wake Parku. Pro další parky
s pravidelnou dobou by se doplnilo generování dnů z šablony — až po pilotu (ADR 0008).
