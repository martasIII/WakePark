namespace WakePark.Modules.Reservations.Domain.Reservations;

/// <summary>Životní cyklus rezervace.</summary>
public enum ReservationStatus
{
    /// <summary>Vytvořena, čeká na zaplacení.</summary>
    Pending = 0,

    /// <summary>Potvrzena — místo je závazně obsazené.</summary>
    Confirmed = 1,

    /// <summary>Zákazník se odbavil na místě.</summary>
    CheckedIn = 2,

    /// <summary>Odjeto.</summary>
    Completed = 3,

    /// <summary>Stornována.</summary>
    Cancelled = 4,

    /// <summary>Zákazník nedorazil.</summary>
    NoShow = 5,
}

/// <summary>Odkud rezervace přišla. Rozhoduje o storno lhůtách i o reportingu.</summary>
public enum ReservationSource
{
    Online = 0,
    Desk = 1,
    Phone = 2,
}
