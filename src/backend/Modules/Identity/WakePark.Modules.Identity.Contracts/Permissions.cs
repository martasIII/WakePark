namespace WakePark.Modules.Identity.Contracts;

/// <summary>
/// Kódy oprávnění. Jsou tady, a ne uvnitř modulu Identity, protože je potřebují všechny
/// moduly při zabezpečení svých endpointů.
/// </summary>
/// <remarks>
/// Konstanta je zdroj pravdy, tabulka <c>permissions</c> je jen zrcadlo pro administraci.
/// Nové oprávnění se přidá sem a seeder ho doplní do databáze.
/// </remarks>
public static class Permissions
{
    public const string ParksRead = "parks.read";
    public const string ParksManage = "parks.manage";

    public const string OperationsRead = "operations.read";
    public const string OperationsManage = "operations.manage";

    public const string BlockingsManage = "blockings.manage";

    public const string ReservationsRead = "reservations.read";
    public const string ReservationsCreate = "reservations.create";
    public const string ReservationsCancel = "reservations.cancel";

    public const string PassesManage = "passes.manage";

    public const string UsersManage = "users.manage";

    public const string CatalogManage = "catalog.manage";

    /// <summary>Všechna známá oprávnění i s popisem. Používá seeder a administrace.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ParksRead] = "Zobrazit areály",
        [ParksManage] = "Spravovat areály a jejich nastavení",
        [OperationsRead] = "Zobrazit rozvrh a provozní dobu",
        [OperationsManage] = "Spravovat vleky, sezóny a provozní dobu",
        [BlockingsManage] = "Blokovat vlek nebo areál (závada, akce, počasí)",
        [ReservationsRead] = "Zobrazit rezervace a obsazenost",
        [ReservationsCreate] = "Vytvářet rezervace",
        [ReservationsCancel] = "Stornovat rezervace",
        [PassesManage] = "Spravovat permanentky a kredit",
        [UsersManage] = "Spravovat uživatele a jejich role",
        [CatalogManage] = "Spravovat produkty, kategorie a ceny",
    };
}

/// <summary>Názvy systémových rolí. Zakládá je seeder, tenant je nemaže.</summary>
public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
    public const string Instructor = "Instructor";

    /// <summary>Které oprávnění patří ke které systémové roli.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Grants { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Admin] = [.. Permissions.All.Keys],
            [Manager] =
            [
                Permissions.ParksRead,
                Permissions.ParksManage,
                Permissions.OperationsRead,
                Permissions.OperationsManage,
                Permissions.BlockingsManage,
                Permissions.ReservationsRead,
                Permissions.ReservationsCreate,
                Permissions.ReservationsCancel,
                Permissions.PassesManage,
                Permissions.CatalogManage,
            ],
            [Employee] =
            [
                Permissions.ParksRead,
                Permissions.OperationsRead,
                Permissions.BlockingsManage,
                Permissions.ReservationsRead,
                Permissions.ReservationsCreate,
                Permissions.ReservationsCancel,
            ],
            [Instructor] =
            [
                Permissions.ParksRead,
                Permissions.OperationsRead,
                Permissions.ReservationsRead,
            ],
        };
}
