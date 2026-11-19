using Shouldly;

using WakePark.Modules.Identity.Contracts;

namespace WakePark.Modules.Identity.UnitTests.Domain;

public sealed class SystemRolesTests
{
    /// <summary>Přehled objednávek obsahuje osobní údaje zákazníků a platby — jen Admin (ADR 0012).</summary>
    [Fact]
    public void Objednavky_vidi_jen_admin()
    {
        SystemRoles.Grants[SystemRoles.Admin].ShouldContain(Permissions.OrdersRead);

        foreach (string role in new[] { SystemRoles.Manager, SystemRoles.Employee, SystemRoles.Instructor })
        {
            SystemRoles.Grants[role].ShouldNotContain(Permissions.OrdersRead, role);
        }
    }

    /// <summary>UAT běží nad kopií ostrých dat (osobní údaje zákazníků) — jen Admin.</summary>
    [Fact]
    public void Do_UAT_smi_jen_admin()
    {
        SystemRoles.Grants[SystemRoles.Admin].ShouldContain(Permissions.UatAccess);

        foreach (string role in new[] { SystemRoles.Manager, SystemRoles.Employee, SystemRoles.Instructor })
        {
            SystemRoles.Grants[role].ShouldNotContain(Permissions.UatAccess, role);
        }
    }
}
