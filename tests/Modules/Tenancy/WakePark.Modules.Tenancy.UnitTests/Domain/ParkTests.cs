using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.ValueObjects;
using WakePark.Modules.Tenancy.Domain.Parks;

namespace WakePark.Modules.Tenancy.UnitTests.Domain;

public sealed class ParkTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();

    private static Address ValidAddress() =>
        new("Pavlovská", "585/1", "Praha", "16200", "cz");

    [Fact]
    public void Create_ulozi_menu_velkymi_pismeny()
    {
        Park park = Park.Create(EnterpriseId, "Wake Džbán", ValidAddress(), "Europe/Prague", "czk");

        park.Currency.ShouldBe("CZK");
    }

    [Fact]
    public void Create_odmitne_neznamou_casovou_zonu()
    {
        DomainException exception = Should.Throw<DomainException>(() =>
            Park.Create(EnterpriseId, "Wake Džbán", ValidAddress(), "Europe/Atlantida", "CZK"));

        exception.Code.ShouldBe("park.unknown_timezone");
    }

    [Fact]
    public void Create_odmitne_areal_bez_provozovatele()
    {
        DomainException exception = Should.Throw<DomainException>(() =>
            Park.Create(Guid.Empty, "Wake Džbán", ValidAddress(), "Europe/Prague", "CZK"));

        exception.Code.ShouldBe("park.enterprise_required");
    }

    [Fact]
    public void SetReservationEmail_normalizuje_na_mala_pismena()
    {
        Park park = Park.Create(EnterpriseId, "Wake Džbán", ValidAddress(), "Europe/Prague", "CZK");

        park.SetReservationEmail("  Rezervace@Wake-Dzban.CZ ");

        park.ReservationEmail.ShouldBe("rezervace@wake-dzban.cz");
    }
}
