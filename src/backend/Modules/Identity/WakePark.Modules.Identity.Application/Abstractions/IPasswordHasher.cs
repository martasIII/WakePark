namespace WakePark.Modules.Identity.Application.Abstractions;

/// <summary>Výsledek ověření hesla proti uloženému otisku.</summary>
public enum PasswordVerification
{
    /// <summary>Heslo nesouhlasí.</summary>
    Failed = 0,

    /// <summary>Heslo souhlasí.</summary>
    Success = 1,

    /// <summary>Heslo souhlasí, ale otisk vznikl starším algoritmem a měl by se přepočítat.</summary>
    SuccessRehashNeeded = 2,
}

/// <summary>
/// Hashování hesel. Abstrakce drží konkrétní algoritmus mimo aplikační vrstvu, takže jeho
/// výměna neznamená zásah do přihlašovací logiky.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string passwordHash, string password);
}
