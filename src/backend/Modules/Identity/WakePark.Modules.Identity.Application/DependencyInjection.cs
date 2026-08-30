using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Identity.Application.Authentication;
using WakePark.Modules.Identity.Application.Authorization;
using WakePark.Modules.Identity.Contracts;

namespace WakePark.Modules.Identity.Application;

/// <summary>Registrace aplikační vrstvy modulu Identity.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommandHandler<SignInCommand, SignedInUser>, SignInCommandHandler>();
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, CurrentUserDto>, GetCurrentUserQueryHandler>();

        return services;
    }
}
