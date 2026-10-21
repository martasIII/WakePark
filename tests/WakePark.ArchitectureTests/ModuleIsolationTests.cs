using System.Reflection;

using NetArchTest.Rules;

using Shouldly;

using ArchTestResult = NetArchTest.Rules.TestResult;

namespace WakePark.ArchitectureTests;

/// <summary>
/// Moduly spolu smí komunikovat výhradně přes projekty <c>*.Contracts</c>.
/// Tohle je jediná věc, která z modulárního monolitu dělá modulární monolit
/// a ne jen monolit s hezkými složkami.
/// </summary>
public sealed class ModuleIsolationTests
{
    /// <summary>Prefix namespace každého modulu a jeho reprezentativní assembly.</summary>
    private static readonly (string Namespace, Assembly Domain, Assembly Contracts)[] Modules =
    [
        (
            "WakePark.Modules.Identity",
            typeof(Modules.Identity.Domain.Users.UserAccount).Assembly,
            typeof(Modules.Identity.Contracts.CurrentUserDto).Assembly
        ),
        (
            "WakePark.Modules.Tenancy",
            typeof(Modules.Tenancy.Domain.Parks.Park).Assembly,
            typeof(Modules.Tenancy.Contracts.ParkDto).Assembly
        ),
        (
            "WakePark.Modules.Operations",
            typeof(Modules.Operations.Domain.Cables.Cable).Assembly,
            typeof(Modules.Operations.Contracts.CableDto).Assembly
        ),
        (
            "WakePark.Modules.Reservations",
            typeof(Modules.Reservations.Domain.Slots.Slot).Assembly,
            typeof(Modules.Reservations.Contracts.ReservationDto).Assembly
        ),
        (
            "WakePark.Modules.Catalog",
            typeof(Modules.Catalog.Domain.Products.Product).Assembly,
            typeof(Modules.Catalog.Contracts.CatalogDto).Assembly
        ),
        (
            "WakePark.Modules.Billing",
            typeof(Modules.Billing.Domain.Orders.Order).Assembly,
            typeof(Modules.Billing.Contracts.OrderDto).Assembly
        ),
        (
            "WakePark.Modules.Notifications",
            typeof(Modules.Notifications.Domain.Emails.EmailMessage).Assembly,
            typeof(Modules.Notifications.Contracts.NotificationsContracts).Assembly
        ),
    ];

    [Fact]
    public void Domena_modulu_nesmi_znat_zadny_jiny_modul()
    {
        foreach ((string moduleNamespace, Assembly domain, _) in Modules)
        {
            string[] forbidden =
            [
                .. Modules
                    .Where(other => !string.Equals(other.Namespace, moduleNamespace, StringComparison.Ordinal))
                    .Select(other => other.Namespace)
            ];

            ArchTestResult result = Types.InAssembly(domain)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Doména modulu {moduleNamespace} sahá na jiný modul: {Failing(result)}");
        }
    }

    [Fact]
    public void Kontrakty_modulu_nesmi_odkazovat_na_jeho_vnitrek()
    {
        foreach ((string moduleNamespace, _, Assembly contracts) in Modules)
        {
            ArchTestResult result = Types.InAssembly(contracts)
                .ShouldNot()
                .HaveDependencyOnAny(
                    $"{moduleNamespace}.Domain",
                    $"{moduleNamespace}.Application",
                    $"{moduleNamespace}.Infrastructure")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Kontrakty modulu {moduleNamespace} odkazují na jeho vnitřek: {Failing(result)}");
        }
    }

    [Fact]
    public void Kontrakty_nesmi_zaviset_na_infrastrukture()
    {
        foreach ((string moduleNamespace, _, Assembly contracts) in Modules)
        {
            ArchTestResult result = Types.InAssembly(contracts)
                .ShouldNot()
                .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Kontrakty modulu {moduleNamespace} tahají infrastrukturu: {Failing(result)}");
        }
    }

    private static string Failing(ArchTestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}
