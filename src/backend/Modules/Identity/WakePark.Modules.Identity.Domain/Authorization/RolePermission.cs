using System.Diagnostics.CodeAnalysis;

namespace WakePark.Modules.Identity.Domain.Authorization;

/// <summary>
/// Vazba role na oprávnění. Nemá vlastní identitu — je určená dvojicí klíčů.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Permission je v této doméně správný název. Pravidlo chrání konvenci "
        + "kolem System.Security.Permission z .NET Frameworku, která se nás netýká.")]
public sealed class RolePermission
{
    private RolePermission()
    {
    }

    internal RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }
}
