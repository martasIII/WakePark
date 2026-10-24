using WakePark.BuildingBlocks.Application.Messaging;

namespace WakePark.Modules.Catalog.Application.Import;

/// <summary>Co import udělá se záznamy, které už v databázi jsou.</summary>
public enum CatalogImportMode
{
    /// <summary>
    /// Jen doplní kategorie a produkty, které v databázi chybí (podle kódu). Existující nemění —
    /// spravuje je backoffice. Tak běží import při každém nasazení.
    /// </summary>
    AddMissing,

    /// <summary>
    /// Srovná databázi se souborem: přepíše texty, pořadí, místa zobrazení a ceny (od dneška),
    /// co v souboru chybí, skryje. Přepíše i úpravy z backoffice — jen ručně a vědomě.
    /// </summary>
    Overwrite,
}

/// <summary>
/// Nahraje katalog ze souboru. Produkt se páruje podle kódu; nic se nemaže — produkt, na který
/// můžou odkazovat prodané poukazy, se nanejvýš skryje.
/// </summary>
public sealed record ImportCatalogCommand(
    Guid EnterpriseId,
    CatalogDefinition Definition,
    CatalogImportMode Mode = CatalogImportMode.AddMissing)
    : ICommand<CatalogImportResult>;

/// <summary>Počty produktů podle toho, co se s nimi při importu stalo.</summary>
public sealed record CatalogImportResult(CatalogImportMode Mode, int Created, int Updated, int Deactivated, int Unchanged)
{
    public override string ToString() =>
        $"režim {Mode}: nové {Created}, změněné {Updated}, skryté {Deactivated}, beze změny {Unchanged}";
}
