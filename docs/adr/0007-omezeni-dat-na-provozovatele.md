# 0007 — Omezení dat na provozovatele v aplikační vrstvě, ne globálním filtrem EF

**Stav:** Přijato · 8. 10. 2026

## Kontext

WakePark se má prodávat dalším parkům. Provozovatel (`Enterprise`) je tenant a jeho data
nesmí vidět zaměstnanec jiného provozovatele.

`datamodel.md` sliboval globální filtr EF Core (`HasQueryFilter` podle `EnterpriseId`).
Nikde implementovaný nebyl. Výpis `GET /api/v1/tenancy/parks` vracel areály **všech**
provozovatelů a `GET /enterprises/{id}/parks` vrátil areály libovolného zadaného provozovatele.

Autorizace to nezachytí a zachytit nemá: u požadavku bez areálu v adrese
`PermissionAuthorizationHandler` pustí každého, kdo má oprávnění v kterémkoli areálu
(„výpis si pak musí data omezit sám“). Ten druhý krok chyběl. Dokud běží jen Wake Park
s.r.o., chyba se neprojeví; s druhým zákazníkem by unikala data mezi firmami.

## Rozhodnutí

**Na provozovatele omezují data dotazy aplikační vrstvy, explicitně.** Handler si vezme
`ITenantContext` (provozovatel z přihlašovací cookie) a podle něj se ptá repozitáře.

- Repozitář o přihlášeném uživateli nic neví. Metoda „všechny areály napříč provozovateli“
  byla odstraněna — co neexistuje, nejde omylem zavolat.
- **Bez provozovatele nic.** Uživatel bez `EnterpriseId` dostane prázdný seznam, ne všechno.
- Cizí provozovatel vrací totéž co neexistující (prázdný seznam), aby z odpovědi nešlo
  poznat, že existuje.
- Hranici hlídají unit testy handlerů (`GetParksQueryHandlerTests`).

## Proč ne globální filtr EF

Globální filtr bere tenanta z přihlášení. Jenže:

1. **Veřejné čtení bez přihlášení přijde brzy.** E-shop a veřejné rezervace (Storefront)
   budou číst areály, produkty a volné sloty anonymně. Tenant tam neurčuje cookie, ale
   doména nebo areál v adrese. Filtr vázaný na přihlášení by jim vrátil prázdno, nebo by
   se musel vypínat (`IgnoreQueryFilters`) — a vypínání bezpečnostního filtru je přesně
   místo, kde se chyby dělají.
2. **Kontrakty mezi moduly.** `IParkDirectory.GetParkAsync(id)` volají Operations
   a Reservations pro konkrétní areál, ke kterému už přístup ověřila autorizace. Skrytý
   filtr by tu měnil chování podle toho, kdo zrovna volá, a chyba by se hledala těžko.
3. Explicitní podmínka je vidět v kódu i v testu. Globální filtr je neviditelný —
   čtenář repozitáře neví, že existuje.

## Důsledky

**Co získáváme.** Pravidlo je čitelné tam, kde se rozhoduje, a otestované bez databáze.
Veřejné čtení půjde přidat bez obcházení filtrů.

**Co ztrácíme.** Pojistku „automaticky“. Každý nový výpis si omezení musí udělat sám —
zapomenutí je možné. Zmírňuje to pravidlo v repozitářích (žádné „všechno napříč
provozovateli“) a review. Kdyby se zapomínalo, je další krok architektonický nebo
integrační test, který projde všechny výpisy s dvěma provozovateli v databázi.

**Kdy to přehodnotit.** Až bude tenant-scoped entit hodně a výpisy se budou množit.
Pak zvážit globální filtr s tenantem z obecnějšího zdroje (přihlášení **nebo** doména),
ne jen z cookie.
