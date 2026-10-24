using FluentValidation;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Common;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Application.Import;

/// <remarks>
/// Celý import je jedna transakce. Když je v souboru chyba, neuloží se nic — napůl
/// naimportovaný katalog by byl horší než starý.
/// </remarks>
internal sealed class ImportCatalogCommandHandler(
    ICatalogRepository repository,
    ICatalogUnitOfWork unitOfWork,
    CatalogCalendar calendar)
    : ICommandHandler<ImportCatalogCommand, CatalogImportResult>
{
    public const string PriceListName = "Ceník";

    private readonly CatalogDefinitionValidator _validator = new();

    public async Task<CatalogImportResult> HandleAsync(
        ImportCatalogCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogDefinition definition = command.Definition;
        await _validator.ValidateAndThrowAsync(definition, cancellationToken).ConfigureAwait(false);

        bool overwrite = command.Mode == CatalogImportMode.Overwrite;
        DateOnly today = calendar.Today;

        Dictionary<string, Guid> categoryIds = await ImportCategoriesAsync(
                command.EnterpriseId, definition.Categories, overwrite, cancellationToken)
            .ConfigureAwait(false);

        PriceList priceList = await GetOrCreatePriceListAsync(command.EnterpriseId, definition, overwrite, today, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, Product> existing = (await repository
                .GetProductsAsync(command.EnterpriseId, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(p => p.Code, StringComparer.Ordinal);

        int created = 0, updated = 0, deactivated = 0, unchanged = 0;

        for (int index = 0; index < definition.Products.Count; index++)
        {
            ProductDefinition item = definition.Products[index];
            ProductDetails details = new(
                categoryIds[item.Category],
                LocalizedText.Create(item.Name),
                item.Description is null ? null : LocalizedText.Create(item.Description),
                item.Badge is null ? null : LocalizedText.Create(item.Badge),
                item.Image,
                index,
                Channels(item.ShowIn));

            if (existing.Remove(item.Code, out Product? product))
            {
                // Existující produkt spravuje backoffice; přepíše se jen na výslovné přání.
                if (!overwrite)
                {
                    unchanged++;
                    continue;
                }

                // Bitový OR, ne ||: všechny metody se musí zavolat vždy.
                bool changed = product.Update(details)
                    | product.Activate()
                    | priceList.SetPrice(product.Id, item.Price, item.VatRate, today, today);

                if (changed)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }
            else
            {
                product = Product.Create(command.EnterpriseId, item.Code, details);
                repository.Add(product);
                priceList.SetPrice(product.Id, item.Price, item.VatRate, today, today);
                created++;
            }
        }

        // Co v souboru není, se při přepisu jen skryje. Na produkt můžou odkazovat prodané poukazy.
        if (overwrite)
        {
            foreach (Product missing in existing.Values)
            {
                if (missing.Deactivate())
                {
                    deactivated++;
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CatalogImportResult(command.Mode, created, updated, deactivated, unchanged);
    }

    private async Task<Dictionary<string, Guid>> ImportCategoriesAsync(
        Guid enterpriseId,
        IReadOnlyList<CategoryDefinition> definitions,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        List<ProductCategory> all = [.. await repository
            .GetCategoriesAsync(enterpriseId, cancellationToken)
            .ConfigureAwait(false)];

        Dictionary<string, ProductCategory> byCode = all.ToDictionary(c => c.Code, StringComparer.Ordinal);
        List<(ProductCategory Category, string? Parent)> placed = [];

        for (int index = 0; index < definitions.Count; index++)
        {
            CategoryDefinition item = definitions[index];
            LocalizedText name = LocalizedText.Create(item.Name);
            LocalizedText? description = item.Description is null ? null : LocalizedText.Create(item.Description);

            if (byCode.TryGetValue(item.Code, out ProductCategory? category))
            {
                if (!overwrite)
                {
                    continue;
                }

                category.Update(name, description, index);
            }
            else
            {
                category = ProductCategory.Create(enterpriseId, item.Code, name, description, index);
                repository.Add(category);
                all.Add(category);
                byCode[item.Code] = category;
            }

            placed.Add((category, item.Parent));
        }

        // Rodiče až když existují všechny kategorie ze souboru — pořadí v souboru pak nehraje roli.
        foreach ((ProductCategory category, string? parent) in placed)
        {
            CategoryTree.Move(category, parent is null ? null : byCode[parent].Id, all);
        }

        // Kategorie, která ze souboru zmizela, zůstává — bez produktů se na webu nezobrazí.
        return byCode.ToDictionary(pair => pair.Key, pair => pair.Value.Id, StringComparer.Ordinal);
    }

    private async Task<PriceList> GetOrCreatePriceListAsync(
        Guid enterpriseId,
        CatalogDefinition definition,
        bool overwrite,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        PriceList? priceList = await repository
            .GetActivePriceListAsync(enterpriseId, cancellationToken)
            .ConfigureAwait(false);

        LocalizedText? title = definition.Title is null ? null : LocalizedText.Create(definition.Title);

        if (priceList is null)
        {
            priceList = PriceList.Create(enterpriseId, PriceListName, definition.Currency, today);
            priceList.SetTitle(title);
            repository.Add(priceList);
        }
        else if (!string.Equals(priceList.Currency, definition.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"Ceník je v {priceList.Currency}, soubor v {definition.Currency}. Změna měny není import, ale nový ceník.");
        }
        else if (overwrite || priceList.Title is null)
        {
            priceList.SetTitle(title);
        }

        return priceList;
    }

    private static ProductChannels Channels(IReadOnlyList<string> showIn) =>
        showIn.Aggregate(ProductChannels.None, (channels, name) => channels | name switch
        {
            "pricelist" => ProductChannels.PriceList,
            "eshop" => ProductChannels.Eshop,
            "pos" => ProductChannels.Pos,
            _ => throw new ValidationException($"Neznámé místo zobrazení '{name}'."),
        });
}
