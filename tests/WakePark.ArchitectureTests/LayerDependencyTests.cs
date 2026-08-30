using System.Reflection;

using NetArchTest.Rules;

using ArchTestResult = NetArchTest.Rules.TestResult;

using Shouldly;

using WakePark.BuildingBlocks.Domain;

namespace WakePark.ArchitectureTests;

/// <summary>
/// Hlídá směr závislostí v clean architecture. Bez těchto testů se hranice vrstev
/// časem rozpadnou a nikdo si toho nevšimne, dokud nebude pozdě.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Entity).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(BuildingBlocks.Application.Abstractions.IClock).Assembly;

    [Fact]
    public void Domena_nesmi_zaviset_na_aplikacni_vrstve_ani_na_infrastrukture()
    {
        ArchTestResult result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "WakePark.BuildingBlocks.Application",
                "WakePark.BuildingBlocks.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FormatFailure(result));
    }

    [Fact]
    public void Aplikacni_vrstva_nesmi_zaviset_na_infrastrukture()
    {
        ArchTestResult result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "WakePark.BuildingBlocks.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FormatFailure(result));
    }

    private static string FormatFailure(ArchTestResult result) =>
        result.FailingTypeNames is null
            ? "Neznámé porušení pravidla."
            : $"Porušují pravidlo: {string.Join(", ", result.FailingTypeNames)}";
}
