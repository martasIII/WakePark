using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Operations.Domain.Cables;

namespace WakePark.Modules.Operations.Application.Schedule;

/// <summary>
/// Implementace veřejného adresáře vleků. Časová zóna se doplňuje z areálu, aby si volající
/// nemusel skládat údaje ze dvou modulů.
/// </summary>
internal sealed class CableDirectory(
    IOperationsRepository repository,
    IParkTimeZoneProvider timeZoneProvider) : ICableDirectory
{
    public async Task<CableDto?> GetCableAsync(Guid cableId, CancellationToken cancellationToken = default)
    {
        Cable? cable = await repository.FindCableAsync(cableId, cancellationToken).ConfigureAwait(false);

        return cable is null
            ? null
            : await ToDtoAsync(cable, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CableDto>> GetCablesAsync(
        Guid wakeParkId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Cable> cables = await repository
            .GetCablesAsync(wakeParkId, cancellationToken)
            .ConfigureAwait(false);

        List<CableDto> result = new(cables.Count);

        foreach (Cable cable in cables)
        {
            result.Add(await ToDtoAsync(cable, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    private async Task<CableDto> ToDtoAsync(Cable cable, CancellationToken cancellationToken)
    {
        TimeZoneInfo timeZone = await timeZoneProvider
            .GetTimeZoneAsync(cable.WakeParkId, cancellationToken)
            .ConfigureAwait(false);

        return new CableDto(
            cable.Id,
            cable.WakeParkId,
            cable.Name,
            cable.Description,
            cable.TowerCount,
            cable.IsActive,
            timeZone.Id);
    }
}
