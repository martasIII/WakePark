namespace WakePark.BuildingBlocks.Domain;

/// <summary>
/// Porušení doménového pravidla. Vede na HTTP 422, nikoli na 500 — není to chyba programu,
/// ale nepřípustný stav podle byznys pravidel.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Strojově čitelný kód chyby, např. <c>slot.capacity_exceeded</c>.</summary>
    public string? Code { get; }
}
