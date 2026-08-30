namespace WakePark.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Entita se sleduje auditně. Hodnoty doplňuje EF interceptor, aplikační kód je nenastavuje.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; set; }

    string? CreatedBy { get; set; }

    DateTimeOffset? UpdatedAtUtc { get; set; }

    string? UpdatedBy { get; set; }
}
