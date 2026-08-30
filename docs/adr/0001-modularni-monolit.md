# 0001 — Modulární monolit místo microservices

**Stav:** Přijato · 30. 8. 2026

## Kontext

Zadání znělo „microservices + gateway". Provoz je jeden až několik wakeparků, řádově desítky
souběžných uživatelů ve špičce. Vývoj dělá jeden člověk.

Rezervace a fakturace jsou transakčně těsně svázané: vytvoření rezervace, odečet kreditu
a vystavení dokladu musí být buď všechno, nebo nic. Rozdělením na samostatné služby by
z jedné databázové transakce vznikla saga s kompenzacemi.

## Rozhodnutí

Stavíme **modulární monolit**: jeden proces, moduly jako samostatné assembly.

- Každý modul má vlastní databázové schéma. Žádné cizí klíče přes hranici modulu.
- Moduly spolu komunikují výhradně přes projekt `*.Contracts` (DTO a integrační události).
- Hranice vynucují testy v `WakePark.ArchitectureTests` — porušení shodí build.
- Před API stojí **YARP gateway** jako jediný vstupní bod pro frontend (CORS, autentizace,
  rate limiting, později BFF).

## Důsledky

**Co získáváme.** Databázové transakce napříč celou operací. Jeden proces k debugování.
Refaktoring hranic modulů je změna kódu, ne migrace nasazení. Jedno nasazení.

**Co ztrácíme.** Moduly nelze škálovat nezávisle. Chyba v jednom modulu shodí celý proces.
Celé řešení je vázané na jeden jazyk a jeden runtime.

**Kdy to přehodnotit.** Až konkrétní modul bude mít výrazně jiný profil zátěže nebo jinou
kadenci nasazování než zbytek. První reální kandidáti na vytažení: `Notifications` (e-mail,
SMS, webhooky platební brány) a `Reporting`. Díky hranicím vynuceným testy je extrakce
mechanická práce, ne přepis.

## Zvažované alternativy

**Microservices od začátku.** Zamítnuto: cena distribuované konzistence, verzovaných kontraktů
a provozu je pro tuto velikost a jednoho vývojáře neúměrná přínosu.

**Klasický monolit bez modulů.** Zamítnuto: bez vynucených hranic se vrstvy do roka slijí
a pozdější rozdělení už nebude proveditelné.
