# ADR 0014 — Akční slevy v e-shopu

**Stav:** Přijato · 19. 11. 2026

## Kontext

Majitel s Danem chtějí v e-shopu dělat časově omezené akce („Vánoční akce −15 % na vouchery“)
a zadávat je sami, bez SQL. Zákazník má vidět původní i akční cenu a název akce.

## Rozhodnutí

- **Akce = procento na kategorii** (platí i pro podkategorie), celé dny od–do včetně v čase
  areálu, přelom o půlnoci. Na jednotlivý produkt zatím ne.
- **Sleva 1–90 %, cena po slevě na celé koruny nahoru** (1 490 Kč − 15 % = 1 267 Kč).
  Sleva, která by po zaokrouhlení cenu nezměnila, se neukazuje.
- **Překryv zakázán:** na jednu kategorii nesmí ve stejný den běžet dvě akce (422 s názvem
  kolidující akce). Zákazník tak vždy ví, která sleva platí.
- **Jen e-shop.** Ceník na webu a pokladna akci neznají.
- **Modul Catalog** (`Promotion`, tabulka `catalog.promotions`). Nabídka e-shopu
  (`CatalogDto`) má u produktu cenu po slevě v `UnitPrice` a původní cenu v
  `RegularUnitPrice`, u kategorie název a konec akce. Billing bere cenu z nabídky jako dosud,
  takže objednávka dostane akční cenu bez změny v Billingu a cenu si zamrazí — pozdější
  změna nebo smazání akce prodané neovlivní.
- **Doklad a e-mail jen s výslednou cenou** (bez původní ceny a slevy).
- **Backoffice „Akční slevy“** (oprávnění `catalog.manage`): probíhající, naplánované a
  nedávno skončené akce; dialog s náhledem cen produktů tak, jak je spočítá e-shop.
- **Web:** pruh s názvem akce, procentem a koncem („dnes je poslední den!“) v kategorii,
  štítek „−15 %“ na produktu, přeškrtnutá původní cena.

### Doplněno 20. 11. 2026 — procento u jednotlivých produktů

- Akce má **výchozí procento** a produkty **přímo v kategorii** (ne v podkategorii) můžou mít
  vlastní, 0–90 %; **0 = produkt v akci není**. Ukládá se jako `product_discounts` (jsonb) u
  akce — čte se jen s ní a výjimek je pár. Výjimka stejná jako výchozí procento se neukládá.
- V dialogu backoffice **změna výchozí slevy nebo kategorie vrátí všechny produkty na
  výchozí** (přání majitele — žádné zapomenuté výjimky ze starého nastavení).
- Na jednu kategorii dál jen jedna akce najednou.
- **Pruh na webu se skládá z toho, co zákazník u produktů opravdu vidí:** nejvyšší procento,
  „až −30 %“ jen když se procenta liší, „na vybrané produkty“ když něco zlevněné není
  (jinak „na celou nabídku níže“). Když po výjimkách a zaokrouhlení nezlevní nic, pruh není.

## Důsledky

- Web drží nabídku minutu v paměti — akce se na webu objeví nejpozději minutu po
  začátku. Objednávka ale cenu počítá vždy znovu na serveru, takže zákazník nikdy nezaplatí
  jinou cenu, než jaká v tu chvíli platí (nejhůř uvidí na minutu starou cenu).
- V přehledu objednávek není vidět, že se prodávalo v akci — jen nižší cena. Kdyby bylo
  potřeba vyhodnocovat akce, přidat k řádku objednávky odkaz na akci (změna v Billingu).
