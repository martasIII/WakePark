using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Tenancy.Application.Abstractions;
using WakePark.Modules.Tenancy.Application.Parks.GetParks;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.UnitTests.Application;

/// <summary>
/// Areály jednoho provozovatele nesmí vidět zaměstnanec jiného. Systém se má prodávat
/// dalším parkům — tohle je hranice mezi zákazníky (ADR 0007).
/// </summary>
public sealed class GetParksQueryHandlerTests
{
    private static readonly Guid MyEnterprise = Guid.Parse("01990000-0000-7000-8000-000000000001");
    private static readonly Guid OtherEnterprise = Guid.Parse("01990000-0000-7000-8000-000000000002");

    private readonly IParkReadRepository _repository = Substitute.For<IParkReadRepository>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();

    public GetParksQueryHandlerTests()
    {
        _repository
            .GetByEnterpriseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => [Park(call.Arg<Guid>())]);
    }

    [Fact]
    public async Task Moje_arealy_jsou_jen_od_meho_provozovatele()
    {
        _tenant.EnterpriseId.Returns(MyEnterprise);

        IReadOnlyList<ParkDto> parks = await new GetTenantParksQueryHandler(_repository, _tenant)
            .HandleAsync(new GetTenantParksQuery(), TestContext.Current.CancellationToken);

        parks.ShouldAllBe(park => park.EnterpriseId == MyEnterprise);
        await _repository.Received(1).GetByEnterpriseAsync(MyEnterprise, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uzivatel_bez_provozovatele_nevidi_nic()
    {
        _tenant.EnterpriseId.Returns((Guid?)null);

        IReadOnlyList<ParkDto> parks = await new GetTenantParksQueryHandler(_repository, _tenant)
            .HandleAsync(new GetTenantParksQuery(), TestContext.Current.CancellationToken);

        parks.ShouldBeEmpty();
        await _repository.DidNotReceiveWithAnyArgs().GetByEnterpriseAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Arealy_ciziho_provozovatele_se_nevrati()
    {
        _tenant.EnterpriseId.Returns(MyEnterprise);

        IReadOnlyList<ParkDto> parks = await new GetParksQueryHandler(_repository, _tenant)
            .HandleAsync(new GetParksQuery(OtherEnterprise), TestContext.Current.CancellationToken);

        parks.ShouldBeEmpty();
        await _repository.DidNotReceiveWithAnyArgs().GetByEnterpriseAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Arealy_vlastniho_provozovatele_se_vrati()
    {
        _tenant.EnterpriseId.Returns(MyEnterprise);

        IReadOnlyList<ParkDto> parks = await new GetParksQueryHandler(_repository, _tenant)
            .HandleAsync(new GetParksQuery(MyEnterprise), TestContext.Current.CancellationToken);

        parks.ShouldHaveSingleItem().EnterpriseId.ShouldBe(MyEnterprise);
    }

    private static ParkDto Park(Guid enterpriseId) => new(
        Guid.CreateVersion7(),
        enterpriseId,
        "Wake Park Džbán",
        null,
        "Praha",
        "CZ",
        "Europe/Prague",
        "CZK",
        new CancellationPolicyDto(true, 24, 50m, 0m));
}
