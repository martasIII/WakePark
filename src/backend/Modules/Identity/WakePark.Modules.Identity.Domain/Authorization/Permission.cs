using System.Diagnostics.CodeAnalysis;

using WakePark.BuildingBlocks.Domain;

namespace WakePark.Modules.Identity.Domain.Authorization;

/// <summary>
/// Jedno konkrétní oprávnění, například <c>reservations.cancel</c>.
/// </summary>
/// <remarks>
/// V náčrtu datového modelu se to jmenovalo <c>PhysicalRole</c>. Není to role, je to úkon,
/// který role povoluje — proto <c>Permission</c> a vazba na roli jako M:N.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Permission je v této doméně správný název. Pravidlo chrání konvenci "
        + "kolem System.Security.Permission z .NET Frameworku, která se nás netýká.")]
public sealed class Permission : Entity
{
    private Permission()
    {
    }

    public Permission(string code, string description)
        : base(NewId())
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Code = code.Trim();
        Description = description.Trim();
    }

    public string Code { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;
}
