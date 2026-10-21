using WakePark.BuildingBlocks.Application.Messaging;

namespace WakePark.Modules.Catalog.Application.Import;

/// <summary>
/// Srovná katalog provozovatele v databázi se souborem. Produkt se páruje podle kódu;
/// co v souboru chybí, se skryje, nikdy nesmaže. Opakovaný import téhož souboru nic nezmění.
/// </summary>
public sealed record ImportCatalogCommand(Guid EnterpriseId, CatalogDefinition Definition)
    : ICommand<CatalogImportResult>;

/// <summary>Počty produktů podle toho, co se s nimi při importu stalo.</summary>
public sealed record CatalogImportResult(int Created, int Updated, int Deactivated, int Unchanged)
{
    public override string ToString() =>
        $"nové {Created}, změněné {Updated}, skryté {Deactivated}, beze změny {Unchanged}";
}
