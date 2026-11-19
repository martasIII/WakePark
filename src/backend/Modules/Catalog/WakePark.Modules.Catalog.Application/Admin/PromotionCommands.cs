using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Domain.Promotions;

namespace WakePark.Modules.Catalog.Application.Admin;

// Akční slevy v e-shopu (ADR 0014). Stejně jako zbytek správy katalogu vrací každý příkaz
// celý katalog po změně.

public sealed record CreatePromotionCommand(PromotionRequest Request) : ICommand<CatalogAdminDto>;

public sealed record UpdatePromotionCommand(Guid PromotionId, PromotionRequest Request) : ICommand<CatalogAdminDto>;

/// <summary>Smazání akce. Prodané objednávky mají cenu zamrazenou, takže je to bezpečné kdykoli.</summary>
public sealed record DeletePromotionCommand(Guid PromotionId) : ICommand<CatalogAdminDto>;

internal sealed class CreatePromotionCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<CreatePromotionCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(CreatePromotionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        PromotionRequest request = command.Request;
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        workspace.Add(Promotion.Create(
            workspace.EnterpriseId,
            CatalogWorkspace.Text(request.Name),
            request.CategoryId,
            request.DiscountPercent,
            request.ValidFrom,
            request.ValidTo,
            workspace.Today));

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class UpdatePromotionCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<UpdatePromotionCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(UpdatePromotionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        PromotionRequest request = command.Request;
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        Promotion promotion = workspace.Promotion(command.PromotionId);
        promotion.Change(
            CatalogWorkspace.Text(request.Name),
            request.CategoryId,
            request.DiscountPercent,
            request.ValidFrom,
            request.ValidTo,
            workspace.Today);
        workspace.EnsurePromotionFits(promotion);

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeletePromotionCommandHandler(CatalogWorkspaceLoader loader)
    : ICommandHandler<DeletePromotionCommand, CatalogAdminDto>
{
    public async Task<CatalogAdminDto> HandleAsync(DeletePromotionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        CatalogWorkspace workspace = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);

        workspace.Remove(workspace.Promotion(command.PromotionId));

        return await workspace.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}
