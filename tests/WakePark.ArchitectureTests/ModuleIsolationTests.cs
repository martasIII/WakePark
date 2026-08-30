using System.Reflection;

using NetArchTest.Rules;

using ArchTestResult = NetArchTest.Rules.TestResult;

using Shouldly;

using WakePark.Modules.Tenancy.Domain.Parks;

namespace WakePark.ArchitectureTests;

/// <summary>
/// Moduly spolu smí komunikovat výhradně přes projekty <c>*.Contracts</c>.
/// Tohle je jediná věc, která z modulárního monolitu dělá modulární monolit
/// a ne jen monolit s hezkými složkami.
/// </summary>
public sealed class ModuleIsolationTests
{
    /// <summary>Namespace prefixy modulů, které v řešení existují.</summary>
    private static readonly string[] ModuleNamespaces = ["WakePark.Modules.Tenancy"];

    [Fact]
    public void Modul_nesmi_zaviset_na_vnitrku_jineho_modulu()
    {
        foreach (string moduleNamespace in ModuleNamespaces)
        {
            Assembly domainAssembly = typeof(Park).Assembly;

            string[] forbidden = ModuleNamespaces
                .Where(other => !string.Equals(other, moduleNamespace, StringComparison.Ordinal))
                .SelectMany(other => new[] { $"{other}.Domain", $"{other}.Application", $"{other}.Infrastructure" })
                .ToArray();

            if (forbidden.Length == 0)
            {
                continue;
            }

            ArchTestResult result = Types.InAssembly(domainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Modul {moduleNamespace} sahá do vnitřku jiného modulu: "
                + string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Fact]
    public void Domenove_entity_modulu_nesmi_opustit_modul_pres_kontrakty()
    {
        ArchTestResult result = Types.InAssembly(typeof(Modules.Tenancy.Contracts.ParkDto).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "WakePark.Modules.Tenancy.Domain",
                "WakePark.Modules.Tenancy.Application",
                "WakePark.Modules.Tenancy.Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Kontrakty modulu nesmí odkazovat na jeho vnitřek: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
