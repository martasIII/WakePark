# 0010 — Gateway zatím nenasazovat, backoffice publikovat přes nginx

**Stav:** Přijato · 26. 10. 2026

## Kontext

ADR 0001 zvolil „modulární monolit + YARP gateway“. Projekt `WakePark.Gateway` vznikl hned
na začátku: jedna trasa `/api/**` → API, CORS pro Angular dev server, `/health`, logování.

Od té doby se ukázalo:

- **Gateway nic nepřidává.** Autentizace, antiforgery a oprávnění jsou v API, rate limiting
  ve storefrontu. Reverzní proxy a TLS na produkci dělá nginx hostitele, který před weby už stojí.
- **CORS v gateway se ani ve vývoji neuplatní.** Angular volá `/api` přes `proxy.conf.json`,
  prohlížeč tedy vidí stejný origin.
- **Na produkci gateway neběží.** Compose má `db`, `api` a `storefront`. Každý kontejner
  navíc znamená paměť (VPS má 2 GB), konfiguraci a další místo, kde se může pokazit předávání
  hlaviček `X-Forwarded-*`.

S backoffice (ADR 0008, 0009) je potřeba rozhodnout, jak se Angular aplikace dostane
k uživatelům a k API.

## Rozhodnutí

**Gateway se zatím nenasazuje. Backoffice publikuje nginx na vlastní subdoméně:**

```
admin.wake-dzban.cz (nginx, TLS)
   ├── /      → statické soubory Angularu (build v obrazu nebo složce na serveru)
   └── /api/  → kontejner API, jen na 127.0.0.1
```

- **Stejný origin pro aplikaci i API:** žádný CORS. Cookie `HttpOnly`, `Secure`,
  `SameSite=Strict` a antiforgery (`XSRF-TOKEN` / `X-XSRF-TOKEN`) fungují beze změny.
- **API musí převzít předávané hlavičky.** Doplnit `UseForwardedHeaders` (`X-Forwarded-For`,
  `X-Forwarded-Proto`) s důvěryhodnou sítí Dockeru, stejně jako má storefront. Bez toho API
  nevidí skutečnou IP návštěvníka (log, budoucí omezení pokusů o přihlášení) a nepozná HTTPS
  (cookie `XSRF-TOKEN` by nedostala příznak `Secure`). Doplněno při nasazení backoffice,
  viz níž.
- **Projekt `WakePark.Gateway` zůstává v repu** jako připravený bod pro budoucnost.
  Neodstraňuje se, ale ani nerozvíjí.
- **Vývoj se nemění:** Angular → `proxy.conf.json` → API. Gateway při vývoji být může, nemusí.

## Kdy rozhodnutí přehodnotit

Gateway dává smysl, když nastane aspoň jedno:

- **Oddělí se modul do samostatné služby** (např. Notifications, pokladna s EET). Gateway pak
  drží jednu adresu pro frontend a směruje podle cesty (strangler pattern).
- **BFF:** gateway drží přihlášení a k API posílá tokeny, protože API začnou volat i mobilní
  aplikace nebo cizí systémy.
- **Víc provozovatelů** (po sezóně 2027): směrování podle domény provozovatele, centrální kvóty.
- **Víc instancí API** s rozkládáním zátěže a kontrolou zdraví instancí.

## Důsledky

**Co získáváme.** O jeden kontejner a jednu vrstvu konfigurace míň. Jeden origin bez CORS.
Využívá se nginx, který na serveru už je a umí TLS i statické soubory.

**Co ztrácíme.** Logika společná pro všechny požadavky (třeba korelační ID) se musí dát do API,
ne do gateway. Při oddělení první služby bude potřeba gateway nasadit a frontend přesměrovat.
Kód gateway na to je připravený.

## Provedení (26. 10. 2026)

Statické soubory obsluhuje vlastní obraz `wakepark-backoffice` (nginx bez práv roota), ne nginx
hostitele — nasazuje se stejně jako ostatní obrazy a verze aplikace sedí s verzí API:

```
internet → nginx hostitele (admin.wake-dzban.cz, TLS) → 127.0.0.1:5082 wakepark-backoffice
                                                           ├── /      → Angular (index.html bez cache)
                                                           └── /api/  → http://api:8080 (síť Dockeru)
```

- Nginx hostitele posílá `X-Forwarded-For: $remote_addr` (hlavičku od klienta nepřebírá),
  nginx v obrazu ji jen předá dál. API věří `X-Forwarded-*` jen ze sítě Dockeru
  (`ForwardedHeaders:TrustedNetworks`).
- `/api/v1/identity/login`: nejvýš 10 pokusů z jedné IP za 10 minut (429), k tomu zamčení
  účtu po 5 špatných heslech. `create-admin` existujícímu účtu nastaví heslo a odemkne ho.
- Klíče přihlašovací cookie jsou ve volume `api-keys`, nasazení nikoho neodhlásí.
