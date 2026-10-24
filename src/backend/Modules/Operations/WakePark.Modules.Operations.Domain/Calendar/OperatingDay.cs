using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Operations.Domain.Calendar;

/// <summary>
/// Otevírací doba areálu v jeden konkrétní den (ADR 0009). Den bez záznamu = zavřeno, takže
/// začátek a konec sezóny jsou prostě první a poslední otevřený den. Provozní dobu Wake Park
/// připravuje na každý den zvlášť podle délky denního světla a zkušeností.
/// </summary>
/// <remarks>Časy jsou v lokálním čase areálu; na okamžiky se převádějí až při plánování slotů.</remarks>
public sealed class OperatingDay : AggregateRoot, IAuditable
{
    private OperatingDay()
    {
    }

    private OperatingDay(Guid id, Guid wakeParkId, DateOnly date)
        : base(id)
    {
        WakeParkId = wakeParkId;
        Date = date;
    }

    public Guid WakeParkId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly OpensAt { get; private set; }

    /// <summary>Zavírá se v tuto chvíli — poslední blok musí skončit nejpozději teď.</summary>
    public TimeOnly ClosesAt { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static OperatingDay Create(Guid wakeParkId, DateOnly date, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (wakeParkId == Guid.Empty)
        {
            throw new DomainException("operating_day.park_required", "Provozní den musí patřit areálu.");
        }

        OperatingDay day = new(NewId(), wakeParkId, date);
        day.SetHours(opensAt, closesAt);
        return day;
    }

    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool SetHours(TimeOnly opensAt, TimeOnly closesAt)
    {
        if (closesAt <= opensAt)
        {
            throw new DomainException("operating_day.invalid_hours", $"{Date:d. M. yyyy}: zavírá se dřív, než se otevírá.");
        }

        if (OpensAt == opensAt && ClosesAt == closesAt)
        {
            return false;
        }

        OpensAt = opensAt;
        ClosesAt = closesAt;
        return true;
    }
}
