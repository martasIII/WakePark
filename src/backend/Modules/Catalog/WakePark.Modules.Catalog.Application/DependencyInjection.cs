using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Catalog;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Catalog.Application;

/// <summary>Registrace aplikační vrstvy modulu Catalog.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IQueryHandler<GetCatalogQuery, CatalogDto>, GetCatalogQueryHandler>();
        services.AddScoped<ICommandHandler<ImportCatalogCommand, CatalogImportResult>, ImportCatalogCommandHandler>();

        return services;
    }
}
