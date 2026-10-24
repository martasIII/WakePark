using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Application.Admin;

// Správa katalogu v backoffice (ADR 0009). Každý příkaz vrací celý katalog po změně —
// je malý a obrazovka admina ho tak nemusí načítat znovu.

/// <summary>Katalog přihlášeného provozovatele pro backoffice.</summary>
public sealed record GetCatalogAdminQuery : IQuery<CatalogAdminDto>;

public sealed record CreateCategoryCommand(CreateCategoryRequest Request) : ICommand<CatalogAdminDto>;

public sealed record UpdateCategoryCommand(Guid CategoryId, UpdateCategoryRequest Request) : ICommand<CatalogAdminDto>;

/// <summary>Smaže prázdnou kategorii (bez produktů a podkategorií).</summary>
public sealed record DeleteCategoryCommand(Guid CategoryId) : ICommand<CatalogAdminDto>;

public sealed record CreateProductCommand(CreateProductRequest Request) : ICommand<CatalogAdminDto>;

public sealed record UpdateProductCommand(Guid ProductId, UpdateProductRequest Request) : ICommand<CatalogAdminDto>;

/// <summary>Nová cena produktu od dneška, nebo od zvoleného dne.</summary>
public sealed record SetProductPriceCommand(Guid ProductId, SetPriceRequest Request) : ICommand<CatalogAdminDto>;

/// <summary>Zruší cenu, která teprve začne platit.</summary>
public sealed record RemoveScheduledPriceCommand(Guid ProductId, DateOnly ValidFrom) : ICommand<CatalogAdminDto>;

public sealed record SetPriceListTitleCommand(SetPriceListTitleRequest Request) : ICommand<CatalogAdminDto>;

internal sealed class GetCatalogAdminQueryHandler(CatalogWorkspaceLoader loader)
    : IQueryHandler<GetCatalogAdminQuery, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(GetCatalogAdminQuery query, CancellationToken cancellationToken = default)
    {
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);
        return workspace.View();
    }
}

internal sealed class CreateCategoryCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<CreateCategoryCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        CreateCategoryRequest request = command.Request;
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        ProductCategory category = ProductCategory.Create(
            workspace.EnterpriseId,
            request.Code,
            CatalogWorkspace.Text(request.Name),
            CatalogWorkspace.OptionalText(request.Description),
            request.SortOrder);

        workspace.Add(category);
        CategoryTree.Move(category, request.ParentId, workspace.Categories);

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class UpdateCategoryCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<UpdateCategoryCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(UpdateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        UpdateCategoryRequest request = command.Request;
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        ProductCategory category = workspace.Category(command.CategoryId);
        category.Update(CatalogWorkspace.Text(request.Name), CatalogWorkspace.OptionalText(request.Description), request.SortOrder);
        CategoryTree.Move(category, request.ParentId, workspace.Categories);

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeleteCategoryCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<DeleteCategoryCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(DeleteCategoryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        workspace.Remove(workspace.Category(command.CategoryId));

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class CreateProductCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<CreateProductCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        CreateProductRequest request = command.Request;
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        Product product = Product.Create(
            workspace.EnterpriseId,
            request.Code,
            new ProductDetails(
                workspace.RequireCategory(request.CategoryId),
                CatalogWorkspace.Text(request.Name),
                CatalogWorkspace.OptionalText(request.Description),
                CatalogWorkspace.OptionalText(request.Badge),
                request.ImagePath,
                request.SortOrder,
                CatalogWorkspace.Channels(request.Visibility)));

        workspace.Add(product);

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class UpdateProductCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<UpdateProductCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        UpdateProductRequest request = command.Request;
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        Product product = workspace.Product(command.ProductId);
        product.Update(new ProductDetails(
            workspace.RequireCategory(request.CategoryId),
            CatalogWorkspace.Text(request.Name),
            CatalogWorkspace.OptionalText(request.Description),
            CatalogWorkspace.OptionalText(request.Badge),
            request.ImagePath,
            request.SortOrder,
            CatalogWorkspace.Channels(request.Visibility)));

        if (request.IsActive)
        {
            product.Activate();
        }
        else
        {
            product.Deactivate();
        }

        workspace.EnsureEshopPricesArePositive(product);

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class SetProductPriceCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<SetProductPriceCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(SetProductPriceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        SetPriceRequest request = command.Request;
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        Product product = workspace.Product(command.ProductId);
        workspace.EnsurePriceList().SetPrice(
            product.Id, request.UnitPrice, request.VatRate, request.ValidFrom ?? workspace.Today, workspace.Today);
        workspace.EnsureEshopPricesArePositive(product);

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class RemoveScheduledPriceCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<RemoveScheduledPriceCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(RemoveScheduledPriceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        Product product = workspace.Product(command.ProductId);
        if (workspace.PriceList?.RemoveScheduledPrice(product.Id, command.ValidFrom, workspace.Today) != true)
        {
            throw new BuildingBlocks.Application.NotFoundException("Cena od tohoto data neexistuje.");
        }

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class SetPriceListTitleCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<SetPriceListTitleCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(SetPriceListTitleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        workspace.EnsurePriceList().SetTitle(CatalogWorkspace.OptionalText(command.Request.Title));

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}
