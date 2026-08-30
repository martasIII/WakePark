namespace WakePark.Modules.Reservations.Domain.Slots;

/// <summary>Stav rezervovatelného bloku.</summary>
public enum SlotStatus
{
    /// <summary>Lze rezervovat.</summary>
    Open = 0,

    /// <summary>Dočasně uzavřen (obsluha, údržba), existující rezervace platí.</summary>
    Closed = 1,

    /// <summary>Zrušen, existující rezervace se stornují.</summary>
    Cancelled = 2,
}
