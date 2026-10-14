using NetArchTest.Rules;

using Shouldly;

using ArchTestResult = NetArchTest.Rules.TestResult;

namespace WakePark.ArchitectureTests;

/// <summary>
/// Veřejný web mluví s backendem jen přes HTTP API, stejně jako Angular. Kdyby sáhl přímo
/// na modul nebo databázi, nešel by nasadit odděleně a obešel by pravidla modulů.
/// </summary>
public sealed class StorefrontIsolationTests
{
    [Fact]
    public void Storefront_nesmi_odkazovat_na_moduly_ani_databazi()
    {
        ArchTestResult result = Types.InAssembly(typeof(Storefront.Content.SiteContent).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "WakePark.Modules",
                "WakePark.BuildingBlocks",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Storefront sahá mimo HTTP API: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
