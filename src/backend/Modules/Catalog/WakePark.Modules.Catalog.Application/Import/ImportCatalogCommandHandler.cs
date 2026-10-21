using FluentValidation;

using WakePark.BuildingBlocks.Application.Abstractions;
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
    IClock clock)
    : ICommandHandler<ImportCatalogCommand, CatalogImportResult>
{
    public const string PriceListName = "E-shop";

    private readonly CatalogDefinitionValidator _validator = new();

    public async Task<CatalogImportResult> HandleAsync(
        ImportCatalogCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogDefinition definition = command.Definition;
        await _validator.ValidateAndThrowAsync(definition, cancellationToken).ConfigureAwait(false);

        Dictionary<string, Guid> categoryIds = await ImportCategoriesAsync(
                command.EnterpriseId, definition.Categories, cancellationToken)
            .ConfigureAwait(false);

        PriceList priceList = await GetOrCreatePriceListAsync(
                command.EnterpriseId, definition.Currency, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, Product> existing = (await repository
                .GetProductsAsync(command.EnterpriseId, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(p => p.Code, StringComparer.Ordinal);

        int created = 0, updated = 0, deactivated = 0, unchanged = 0;

        for (int index = 0; index < definition.Products.Count; index++)
        {
            ProductDefinition item = definition.Products[index];
            Guid categoryId = categoryIds[item.Category];
            LocalizedText name = LocalizedText.Create(item.Name);
            LocalizedText description = LocalizedText.Create(item.Description);

            if (existing.Remove(item.Code, out Product? product))
            {
                // Bitový OR, ne ||: obě metody se musí zavolat vždy.
                bool changed = product.Update(categoryId, name, description, item.Image, index)
                    | product.Activate()
                    | priceList.SetPrice(product.Id, item.Price, item.VatRate);

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
                product = Product.Create(
                    command.EnterpriseId, item.Code, categoryId, name, description, item.Image, index);
                repository.Add(product);
                priceList.SetPrice(product.Id, item.Price, item.VatRate);
                created++;
            }
        }

        // Co v souboru není, se jen skryje. Na produkt můžou odkazovat prodané poukazy.
        foreach (Product missing in existing.Values)
        {
            if (missing.Deactivate())
            {
                deactivated++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CatalogImportResult(created, updated, deactivated, unchanged);
    }

    private async Task<Dictionary<string, Guid>> ImportCategoriesAsync(
        Guid enterpriseId,
        IReadOnlyList<CategoryDefinition> definitions,
        CancellationToken cancellationToken)
    {
        Dictionary<string, ProductCategory> existing = (await repository
                .GetCategoriesAsync(enterpriseId, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(c => c.Code, StringComparer.Ordinal);

        Dictionary<string, Guid> ids = new(StringComparer.Ordinal);

        for (int index = 0; index < definitions.Count; index++)
        {
            CategoryDefinition item = definitions[index];
            LocalizedText name = LocalizedText.Create(item.Name);

            if (existing.TryGetValue(item.Code, out ProductCategory? category))
            {
                category.Update(name, index);
            }
            else
            {
                category = ProductCategory.Create(enterpriseId, item.Code, name, index);
                repository.Add(category);
            }

            ids[item.Code] = category.Id;
        }

        // Kategorie, která ze souboru zmizela, zůstává — bez aktivních produktů se na webu nezobrazí.
        return ids;
    }

    private async Task<PriceList> GetOrCreatePriceListAsync(
        Guid enterpriseId,
        string currency,
        CancellationToken cancellationToken)
    {
        PriceList? priceList = await repository
            .GetActivePriceListAsync(enterpriseId, cancellationToken)
            .ConfigureAwait(false);

        if (priceList is null)
        {
            priceList = PriceList.Create(
                enterpriseId, PriceListName, currency, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
            repository.Add(priceList);
        }
        else if (!string.Equals(priceList.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"Ceník je v {priceList.Currency}, soubor v {currency}. Změna měny není import, ale nový ceník.");
        }

        return priceList;
    }
}
