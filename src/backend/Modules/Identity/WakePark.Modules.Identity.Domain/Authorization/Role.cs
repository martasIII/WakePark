using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Identity.Domain.Authorization;

/// <summary>
/// Logická role, například Admin nebo Employee. Oprávnění nese přes <see cref="RolePermission"/>.
/// </summary>
public sealed class Role : AggregateRoot, IAuditable
{
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    private Role(Guid id, Guid? enterpriseId, string name, string description)
        : base(id)
    {
        EnterpriseId = enterpriseId;
        Name = name;
        Description = description;
    }

    /// <summary>Null u systémové role. Vlastní role tenanta nesou jeho identifikátor.</summary>
    public Guid? EnterpriseId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsSystemRole => EnterpriseId is null;

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    /// <summary>Systémová role, kterou zakládá seeder a tenant ji nemění.</summary>
    public static Role System(string name, string description) =>
        new(NewId(), enterpriseId: null, Guard(name), Guard(description));

    /// <summary>Vlastní role konkrétního provozovatele.</summary>
    public static Role ForEnterprise(Guid enterpriseId, string name, string description)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("role.enterprise_required", "Vlastní role musí patřit provozovateli.");
        }

        return new Role(NewId(), enterpriseId, Guard(name), Guard(description));
    }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public void Grant(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
        {
            throw new DomainException("role.permission_required", "Oprávnění musí mít identifikátor.");
        }

        if (_permissions.Exists(p => p.PermissionId == permissionId))
        {
            return;
        }

        _permissions.Add(new RolePermission(Id, permissionId));
    }

    public void Revoke(Guid permissionId) => _permissions.RemoveAll(p => p.PermissionId == permissionId);

    private static string Guard(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
