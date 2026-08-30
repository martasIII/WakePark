using Shouldly;

using WakePark.Modules.Identity.Application.Authorization;
using WakePark.Modules.Identity.Contracts;

namespace WakePark.Modules.Identity.UnitTests.Application;

/// <summary>
/// Oprávnění jsou vázaná na areál. Tyhle testy hlídají, že se kontrola nedá obejít
/// oprávněním získaným v jiném parku.
/// </summary>
public sealed class PermissionCheckerTests
{
    private static readonly Guid ParkA = Guid.CreateVersion7();
    private static readonly Guid ParkB = Guid.CreateVersion7();

    private static CurrentUserDto Access(
        IReadOnlyList<ParkAccessDto>? parks = null,
        IReadOnlyList<string>? global = null) =>
        new(Guid.CreateVersion7(), "a@b.cz", Guid.CreateVersion7(), parks ?? [], global ?? []);

    [Fact]
    public void Opravneni_v_arealu_plati_v_tom_arealu()
    {
        CurrentUserDto access = Access([new ParkAccessDto(ParkA, ["Employee"], [Permissions.ReservationsCreate])]);

        PermissionChecker.Has(access, Permissions.ReservationsCreate, ParkA).ShouldBeTrue();
    }

    [Fact]
    public void Opravneni_z_jineho_arealu_neplati()
    {
        CurrentUserDto access = Access([new ParkAccessDto(ParkA, ["Employee"], [Permissions.ReservationsCreate])]);

        PermissionChecker.Has(access, Permissions.ReservationsCreate, ParkB).ShouldBeFalse();
    }

    [Fact]
    public void Opravneni_pro_celeho_provozovatele_plati_vsude()
    {
        CurrentUserDto access = Access(global: [Permissions.ReservationsCreate]);

        PermissionChecker.Has(access, Permissions.ReservationsCreate, ParkB).ShouldBeTrue();
    }

    [Fact]
    public void Bez_urceneho_arealu_staci_opravneni_kdekoli()
    {
        CurrentUserDto access = Access([new ParkAccessDto(ParkA, ["Employee"], [Permissions.ReservationsRead])]);

        PermissionChecker.Has(access, Permissions.ReservationsRead, wakeParkId: null).ShouldBeTrue();
    }

    [Fact]
    public void Jine_opravneni_v_temze_arealu_nestaci()
    {
        CurrentUserDto access = Access([new ParkAccessDto(ParkA, ["Instructor"], [Permissions.ReservationsRead])]);

        PermissionChecker.Has(access, Permissions.ReservationsCancel, ParkA).ShouldBeFalse();
    }

    [Fact]
    public void Uzivatel_bez_roli_nesmi_nic()
    {
        PermissionChecker.Has(Access(), Permissions.ParksRead, ParkA).ShouldBeFalse();
        PermissionChecker.Has(Access(), Permissions.ParksRead, wakeParkId: null).ShouldBeFalse();
    }
}
