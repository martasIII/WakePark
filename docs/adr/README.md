# Architektonická rozhodnutí (ADR)

Každý soubor zaznamenává jedno rozhodnutí: co se rozhodlo, proč, a co to stojí.
Smysl není mít hezkou dokumentaci — je to proto, aby se za rok nediskutovalo znovu to samé
bez znalosti původních důvodů.

Rozhodnutí se **nepřepisují**. Když se změní, přidá se nové ADR, které to staré nahrazuje.

| # | Rozhodnutí | Stav |
|---|---|---|
| [0001](0001-modularni-monolit.md) | Modulární monolit místo microservices | Přijato |
| [0002](0002-postgresql.md) | PostgreSQL a míra nezávislosti na DB enginu | Přijato |
| [0003](0003-dotnet-10.md) | .NET 10 místo .NET 8 | Přijato |
| [0004](0004-cas-a-penize.md) | Práce s časem a penězi | Přijato |
| [0005](0005-kde-zije-slot.md) | Slot patří do modulu Reservations, ne Operations | Přijato |
| [0006](0006-eshop-poukazy.md) | E-shop s poukazy: moduly, platba QR převodem, veřejný web | Navrženo |
| [0007](0007-omezeni-dat-na-provozovatele.md) | Omezení dat na provozovatele v aplikační vrstvě, ne globálním filtrem EF | Přijato |
| [0008](0008-backoffice-mapa-modulu.md) | Backoffice: mapa modulů, dva pohledy a pořadí | Přijato |
| [0009](0009-provoz-a-katalog-v-backoffice.md) | Provozní doba po dnech, blokace a katalog v backoffice | Přijato |
| [0010](0010-gateway-zatim-ne-backoffice-pres-nginx.md) | Gateway zatím nenasazovat, backoffice publikovat přes nginx | Přijato |
| [0011](0011-backoffice-angular-material-typy-z-openapi.md) | Backoffice: Angular Material, typy z OpenAPI, rozložení podle rolí | Přijato |
