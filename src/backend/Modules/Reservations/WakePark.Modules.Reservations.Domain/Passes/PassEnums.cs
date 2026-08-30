namespace WakePark.Modules.Reservations.Domain.Passes;

/// <summary>Druh permanentky.</summary>
public enum RidePassKind
{
    /// <summary>Předplacený počet jízd. Každá rezervace kredit snižuje.</summary>
    RideCredit = 0,

    /// <summary>Sezónní permanentka — neomezeně jízd v období platnosti, kredit se nečerpá.</summary>
    SeasonPass = 1,
}

/// <summary>Stav permanentky.</summary>
public enum RidePassStatus
{
    Active = 0,
    Expired = 1,
    Suspended = 2,
    Refunded = 3,
}

/// <summary>Důvod pohybu kreditu. Slouží k dohledání, proč zůstatek vypadá, jak vypadá.</summary>
public enum CreditReason
{
    /// <summary>Nákup nebo dobití.</summary>
    Purchase = 0,

    /// <summary>Čerpání rezervací.</summary>
    RideConsumed = 1,

    /// <summary>Vrácení po stornu.</summary>
    Refund = 2,

    /// <summary>Ruční oprava obsluhou.</summary>
    ManualAdjustment = 3,

    /// <summary>Propadnutí po vypršení platnosti.</summary>
    Expiry = 4,
}
