using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Contracts;

namespace WakePark.Modules.Identity.Application.Authorization;

/// <summary>Kdo je přihlášený a co smí.</summary>
public sealed record GetCurrentUserQuery(Guid UserAccountId) : IQuery<CurrentUserDto>;

internal sealed class GetCurrentUserQueryHandler(IUserAccessReader reader)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<CurrentUserDto> HandleAsync(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await reader.GetAccessAsync(query.UserAccountId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("user_account.not_found", "Účet neexistuje.");
    }
}
