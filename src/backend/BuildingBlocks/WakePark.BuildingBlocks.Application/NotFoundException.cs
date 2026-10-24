namespace WakePark.BuildingBlocks.Application;

/// <summary>
/// Hledaný záznam neexistuje — nebo patří jinému provozovateli, což navenek vypadá stejně
/// (ADR 0007). API ho překládá na 404.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
