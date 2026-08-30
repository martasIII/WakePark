namespace WakePark.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Zdroj času. Doména ani aplikační vrstva nikdy nesahá na <c>DateTimeOffset.UtcNow</c> přímo —
/// jinak nejde otestovat nic, co závisí na čase.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
