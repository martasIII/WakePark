using WakePark.BuildingBlocks.Application.Abstractions;

namespace WakePark.BuildingBlocks.Infrastructure.Time;

/// <summary>Systémový čas. Jediné místo v celém řešení, kde se čte reálný čas.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
