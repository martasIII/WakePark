namespace WakePark.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Entita patří konkrétnímu provozovateli. Globální query filter podle <see cref="EnterpriseId"/>
/// zajišťuje, že se data tenantů nikdy nepotkají.
/// </summary>
public interface ITenantOwned
{
    Guid EnterpriseId { get; }
}
