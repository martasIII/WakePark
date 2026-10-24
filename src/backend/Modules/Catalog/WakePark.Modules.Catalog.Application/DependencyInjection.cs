using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Admin;
using WakePark.Modules.Catalog.Application.Catalog;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Catalog.Application;

/// <summary>Registrace aplikační vrstvy modulu Catalog. <see cref="CatalogOptions"/> registruje infrastruktura.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<CatalogCalendar>();
        services.AddScoped<ICatalogReader, CatalogReader>();
        services.AddScoped<IQueryHandler<GetCatalogQuery, CatalogDto>, GetCatalogQueryHandler>();
        services.AddScoped<IQueryHandler<GetPublicPriceListQuery, PublicPriceListDto>, GetPublicPriceListQueryHandler>();
        services.AddScoped<ICommandHandler<ImportCatalogCommand, CatalogImportResult>, ImportCatalogCommandHandler>();

        services.AddScoped<CatalogWorkspaceLoader>();
        services.AddScoped<IQueryHandler<GetCatalogAdminQuery, CatalogAdminDto>, GetCatalogAdminQueryHandler>();
        services.AddScoped<ICommandHandler<CreateCategoryCommand, CatalogAdminDto>, CreateCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCategoryCommand, CatalogAdminDto>, UpdateCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteCategoryCommand, CatalogAdminDto>, DeleteCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<CreateProductCommand, CatalogAdminDto>, CreateProductCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateProductCommand, CatalogAdminDto>, UpdateProductCommandHandler>();
        services.AddScoped<ICommandHandler<SetProductPriceCommand, CatalogAdminDto>, SetProductPriceCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveScheduledPriceCommand, CatalogAdminDto>, RemoveScheduledPriceCommandHandler>();
        services.AddScoped<ICommandHandler<SetPriceListTitleCommand, CatalogAdminDto>, SetPriceListTitleCommandHandler>();

        return services;
    }
}
