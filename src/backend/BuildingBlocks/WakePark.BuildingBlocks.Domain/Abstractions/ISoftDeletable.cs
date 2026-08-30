namespace WakePark.BuildingBlocks.Domain.Abstractions;

/// <summary>Entita se nemaže fyzicky, jen se označí jako smazaná. Neplatí pro finanční doklady.</summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAtUtc { get; set; }

    string? DeletedBy { get; set; }
}
