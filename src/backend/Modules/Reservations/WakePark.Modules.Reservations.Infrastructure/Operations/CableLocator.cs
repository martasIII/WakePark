using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Reservations.Application.Abstractions;

namespace WakePark.Modules.Reservations.Infrastructure.Operations;

/// <summary>
/// Zjistí areál vleku přes veřejný kontrakt modulu Operations.
/// </summary>
internal sealed class CableLocator(ICableDirectory cableDirectory) : ICableLocator
{
    public async Task<Guid?> FindWakeParkIdAsync(Guid cableId, CancellationToken cancellationToken = default)
    {
        CableDto? cable = await cableDirectory
            .GetCableAsync(cableId, cancellationToken)
            .ConfigureAwait(false);

        return cable?.WakeParkId;
    }
}
