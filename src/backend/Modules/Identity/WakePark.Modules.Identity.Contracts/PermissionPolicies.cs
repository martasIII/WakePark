namespace WakePark.Modules.Identity.Contracts;

/// <summary>
/// Převod kódu oprávnění na název autorizační politiky.
/// </summary>
/// <remarks>
/// Je to v kontraktu, protože zabezpečení endpointů si dělá každý modul sám a nesmí kvůli
/// tomu sahat do hostu ani do vnitřku modulu Identity.
/// </remarks>
public static class PermissionPolicies
{
    /// <summary>Předpona odlišující dynamické politiky od pojmenovaných.</summary>
    public const string Prefix = "perm:";

    /// <summary>Název politiky vyžadující dané oprávnění.</summary>
    public static string For(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return Prefix + permission;
    }
}
