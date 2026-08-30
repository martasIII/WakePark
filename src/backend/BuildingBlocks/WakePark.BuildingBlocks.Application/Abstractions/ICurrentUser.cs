namespace WakePark.BuildingBlocks.Application.Abstractions;

/// <summary>Kdo právě provádí operaci. Používá se pro audit a autorizaci.</summary>
public interface ICurrentUser
{
    Guid? UserAccountId { get; }

    string? DisplayName { get; }

    bool IsAuthenticated { get; }
}
