using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.ValueObjects;

namespace WakePark.Modules.Tenancy.Domain.Parks;

/// <summary>
/// Pravidla pro storno rezervace. Patří areálu, ne aplikaci — každý park má jinou politiku
/// a nastavuje si ji sám.
/// </summary>
public sealed class CancellationPolicy : ValueObject
{
    private CancellationPolicy()
    {
    }

    public CancellationPolicy(
        bool cancellationAllowed,
        int freeCancellationHours,
        decimal lateCancellationRefundPercent,
        decimal noShowRefundPercent)
    {
        if (freeCancellationHours < 0)
        {
            throw new DomainException(
                "cancellation_policy.invalid_hours",
                "Lhůta pro bezplatné storno nesmí být záporná.");
        }

        EnsurePercent(lateCancellationRefundPercent, nameof(lateCancellationRefundPercent));
        EnsurePercent(noShowRefundPercent, nameof(noShowRefundPercent));

        CancellationAllowed = cancellationAllowed;
        FreeCancellationHours = freeCancellationHours;
        LateCancellationRefundPercent = lateCancellationRefundPercent;
        NoShowRefundPercent = noShowRefundPercent;
    }

    /// <summary>Výchozí politika: storno zdarma do 24 hodin, potom se nevrací nic.</summary>
    public static CancellationPolicy Default { get; } = new(
        cancellationAllowed: true,
        freeCancellationHours: 24,
        lateCancellationRefundPercent: 0m,
        noShowRefundPercent: 0m);

    public bool CancellationAllowed { get; private init; }

    /// <summary>Kolik hodin před začátkem slotu lze stornovat bez sankce.</summary>
    public int FreeCancellationHours { get; private init; }

    /// <summary>Kolik procent se vrací při pozdním stornu.</summary>
    public decimal LateCancellationRefundPercent { get; private init; }

    /// <summary>Kolik procent se vrací, když zákazník nedorazí.</summary>
    public decimal NoShowRefundPercent { get; private init; }

    /// <summary>
    /// Kolik procent se vrátí při stornu provedeném v <paramref name="cancelledAtUtc"/>
    /// u slotu začínajícího v <paramref name="slotStartUtc"/>.
    /// </summary>
    public decimal RefundPercentFor(DateTimeOffset cancelledAtUtc, DateTimeOffset slotStartUtc)
    {
        if (!CancellationAllowed)
        {
            throw new DomainException(
                "reservation.cancellation_not_allowed",
                "Areál storno rezervací neumožňuje.");
        }

        TimeSpan notice = slotStartUtc - cancelledAtUtc;

        return notice >= TimeSpan.FromHours(FreeCancellationHours)
            ? 100m
            : LateCancellationRefundPercent;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CancellationAllowed;
        yield return FreeCancellationHours;
        yield return LateCancellationRefundPercent;
        yield return NoShowRefundPercent;
    }

    private static void EnsurePercent(decimal value, string parameterName)
    {
        if (value is < 0m or > 100m)
        {
            throw new DomainException(
                "cancellation_policy.invalid_percent",
                $"Hodnota '{parameterName}' musí být mezi 0 a 100.");
        }
    }
}
