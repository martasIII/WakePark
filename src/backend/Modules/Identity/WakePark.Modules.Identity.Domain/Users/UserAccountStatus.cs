namespace WakePark.Modules.Identity.Domain.Users;

/// <summary>Stav přihlašovacího účtu.</summary>
public enum UserAccountStatus
{
    /// <summary>Založen, ale ještě nepotvrzen.</summary>
    Pending = 0,

    /// <summary>Může se přihlásit.</summary>
    Active = 1,

    /// <summary>Dočasně pozastaven obsluhou.</summary>
    Suspended = 2,

    /// <summary>Zamčen po opakovaně chybném hesle.</summary>
    Locked = 3,
}
