using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Identity.Domain.Authorization;

/// <summary>
/// Kdo má jakou roli a kde.
/// </summary>
/// <remarks>
/// Oprávnění nejsou globální, ale vázaná na areál: brigádník může být zaměstnancem v jednom
/// parku a běžným zákazníkem v druhém. Otázka „je admin?" proto nemá smysl bez kontextu areálu.
///
/// Oproti náčrtu je <see cref="WakeParkId"/> volitelný. Null znamená, že role platí ve všech
/// areálech provozovatele — bez toho by nešlo založit prvního správce, který teprve areály
/// zakládá.
/// </remarks>
public sealed class UserParkRole : AggregateRoot, IAuditable
{
    private UserParkRole()
    {
    }

    private UserParkRole(
        Guid id,
        Guid userAccountId,
        Guid enterpriseId,
        Guid? wakeParkId,
        Guid roleId,
        DateTimeOffset validFromUtc)
        : base(id)
    {
        UserAccountId = userAccountId;
        EnterpriseId = enterpriseId;
        WakeParkId = wakeParkId;
        RoleId = roleId;
        ValidFromUtc = validFromUtc;
    }

    public Guid UserAccountId { get; private set; }

    public Guid EnterpriseId { get; private set; }

    /// <summary>Null = role platí ve všech areálech provozovatele.</summary>
    public Guid? WakeParkId { get; private set; }

    public Guid RoleId { get; private set; }

    public DateTimeOffset ValidFromUtc { get; private set; }

    public DateTimeOffset? ValidToUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static UserParkRole ForPark(
        Guid userAccountId,
        Guid enterpriseId,
        Guid wakeParkId,
        Guid roleId,
        DateTimeOffset validFromUtc)
    {
        if (wakeParkId == Guid.Empty)
        {
            throw new DomainException("user_park_role.park_required", "Chybí areál.");
        }

        return Create(userAccountId, enterpriseId, wakeParkId, roleId, validFromUtc);
    }

    public static UserParkRole ForWholeEnterprise(
        Guid userAccountId,
        Guid enterpriseId,
        Guid roleId,
        DateTimeOffset validFromUtc) =>
        Create(userAccountId, enterpriseId, wakeParkId: null, roleId, validFromUtc);

    public bool IsValidAt(DateTimeOffset atUtc) =>
        atUtc >= ValidFromUtc && (ValidToUtc is null || atUtc < ValidToUtc);

    public void Revoke(DateTimeOffset atUtc) => ValidToUtc = atUtc;

    private static UserParkRole Create(
        Guid userAccountId,
        Guid enterpriseId,
        Guid? wakeParkId,
        Guid roleId,
        DateTimeOffset validFromUtc)
    {
        if (userAccountId == Guid.Empty)
        {
            throw new DomainException("user_park_role.user_required", "Chybí uživatel.");
        }

        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("user_park_role.enterprise_required", "Chybí provozovatel.");
        }

        if (roleId == Guid.Empty)
        {
            throw new DomainException("user_park_role.role_required", "Chybí role.");
        }

        return new UserParkRole(NewId(), userAccountId, enterpriseId, wakeParkId, roleId, validFromUtc);
    }
}
