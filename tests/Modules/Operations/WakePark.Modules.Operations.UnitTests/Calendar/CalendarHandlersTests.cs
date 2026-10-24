using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application;
using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Application.Calendar;
using WakePark.Modules.Operations.Application.Schedule;
using WakePark.Modules.Operations.Application.Setup;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Calendar;
using WakePark.Modules.Operations.Domain.Parks;

namespace WakePark.Modules.Operations.UnitTests.Calendar;

/// <summary>Provozní kalendář z pohledu backoffice, webu a rezervací (ADR 0009).</summary>
public sealed class CalendarHandlersTests
{
    private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly Guid ParkId = Guid.CreateVersion7();
    private static readonly Guid ForeignParkId = Guid.CreateVersion7();

    private readonly InMemoryOperationsRepository _repository = new();
    private readonly IOperationsUnitOfWork _unitOfWork = Substitute.For<IOperationsUnitOfWork>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IParkLookup _parks = Substitute.For<IParkLookup>();

    public CalendarHandlersTests()
    {
        _tenant.EnterpriseId.Returns(EnterpriseId);
        _clock.UtcNow.Returns(new DateTimeOffset(2027, 4, 30, 10, 0, 0, TimeSpan.Zero));
        _parks.FindAsync(ParkId, Arg.Any<CancellationToken>()).Returns(new ParkInfo(ParkId, EnterpriseId, Prague));
        _parks.FindAsync(ForeignParkId, Arg.Any<CancellationToken>()).Returns(new ParkInfo(ForeignParkId, Guid.CreateVersion7(), Prague));
    }

    [Fact]
    public async Task Import_CSV_ulozi_dny_a_napodruhe_uz_nic_nemeni()
    {
        string csv = "datum;od;do\n1.5.2027;10:00;19:30\n2.5.2027;10:00;19:30\n";

        OperatingDaysImportDto first = await ApplyAsync(csv);
        OperatingDaysImportDto second = await PreviewAsync(csv);

        first.Added.ShouldBe(2);
        _repository.Days.Count.ShouldBe(2);
        second.Unchanged.ShouldBe(2);
        second.Added.ShouldBe(0);
    }

    [Fact]
    public async Task Nahled_nic_neulozi_a_chybny_soubor_neulozi_ani_import()
    {
        OperatingDaysImportDto preview = await PreviewAsync("1.5.2027;10:00;19:30\n");
        preview.Added.ShouldBe(1);
        _repository.Days.ShouldBeEmpty();

        DomainException error = await Should.ThrowAsync<DomainException>(() => ApplyAsync("1.5.2027;10:00;19:30\n2.5.2027;19:00;10:00\n"));

        error.Code.ShouldBe("operating_days.invalid_csv");
        _repository.Days.ShouldBeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cizi_areal_vypada_jako_neexistujici()
    {
        await Should.ThrowAsync<NotFoundException>(() =>
            new ApplyOperatingDaysImportCommandHandler(_repository, _unitOfWork, Access())
                .HandleAsync(new ApplyOperatingDaysImportCommand(ForeignParkId, "1.5.2027;10:00;19:30"), TestContext.Current.CancellationToken));

        _tenant.EnterpriseId.Returns((Guid?)null);
        await Should.ThrowAsync<NotFoundException>(() => ApplyAsync("1.5.2027;10:00;19:30"));
    }

    [Fact]
    public async Task Web_vidi_14_dni_s_blokaci_orezanou_na_otevraci_dobu_a_bez_poznamky()
    {
        await ApplyAsync("30.4.2027;10:00;19:00\n1.5.2027;10:00;19:00\n");
        Cable cable = Cable.Create(ParkId, "vlek-1", "Vlek 1", 2);
        _repository.Add(cable);
        _repository.Add(ParkSettings.Create(ParkId, 20, 3, 14, 30));
        await CreateBlockingAsync(new CreateBlockingRequest(
            cable.Id, new DateTime(2027, 4, 30, 8, 0, 0), new DateTime(2027, 4, 30, 12, 0, 0), "PrivateEvent", "Firma XY, faktura předem"));

        PublicOpeningHoursDto hours = await new GetPublicOpeningHoursQueryHandler(_repository, Access())
            .HandleAsync(new GetPublicOpeningHoursQuery(ParkId), TestContext.Current.CancellationToken);

        hours.Days.Count.ShouldBe(14);
        PublicOpeningDayDto today = hours.Days[0];
        today.Date.ShouldBe(new DateOnly(2027, 4, 30));
        today.OpensAt.ShouldBe(new TimeOnly(10, 0));
        today.Blockings.ShouldBe([new PublicBlockingDto("Vlek 1", new TimeOnly(10, 0), new TimeOnly(12, 0), "PrivateEvent")]);
        hours.Days[2].OpensAt.ShouldBeNull("2. 5. v CSV není = zavřeno");
    }

    [Fact]
    public async Task Blokace_ciziho_vleku_ani_neznamy_duvod_neprojdou()
    {
        Cable foreign = Cable.Create(ForeignParkId, "vlek-1", "Cizí", 2);
        _repository.Add(foreign);

        (await Should.ThrowAsync<DomainException>(() => CreateBlockingAsync(new CreateBlockingRequest(
                foreign.Id, new DateTime(2027, 5, 1, 10, 0, 0), new DateTime(2027, 5, 1, 11, 0, 0), "Weather", null))))
            .Code.ShouldBe("blocking.cable_not_found");

        (await Should.ThrowAsync<DomainException>(() => CreateBlockingAsync(new CreateBlockingRequest(
                null, new DateTime(2027, 5, 1, 10, 0, 0), new DateTime(2027, 5, 1, 11, 0, 0), "Bouřka", null))))
            .Code.ShouldBe("blocking.invalid_category");
    }

    [Fact]
    public async Task Rozvrh_pro_rezervace_vynecha_blokovane_bloky()
    {
        Cable cable = Cable.Create(ParkId, "vlek-1", "Vlek 1", 2);
        _repository.Add(cable);
        _repository.Add(ParkSettings.Create(ParkId, 20, 3, 14, 30));
        await ApplyAsync("1.5.2027;10:00;11:00\n");
        await CreateBlockingAsync(new CreateBlockingRequest(
            null, new DateTime(2027, 5, 1, 10, 20, 0), new DateTime(2027, 5, 1, 10, 40, 0), "TechnicalFault", null));

        IParkTimeZoneProvider timeZones = Substitute.For<IParkTimeZoneProvider>();
        timeZones.GetTimeZoneAsync(ParkId, Arg.Any<CancellationToken>()).Returns(Prague);

        IReadOnlyList<SlotTemplateDto> slots = await new ScheduleService(_repository, timeZones)
            .GetSlotTemplatesAsync(cable.Id, new DateOnly(2027, 5, 1), TestContext.Current.CancellationToken);

        // 10:00 a 10:40 lokálně (letní čas, UTC+2); 10:20 je blokovaný.
        slots.Select(s => s.StartUtc.Hour * 100 + s.StartUtc.Minute).ShouldBe([800, 840]);
    }

    [Fact]
    public async Task Vychozi_data_srovnaji_vleky_podle_kodu_a_chybejici_vyradi()
    {
        ApplyOperationsSetupCommandHandler handler = new(_repository, _unitOfWork);
        OperationsSetup setup = new(
            new ParkSettingsSetup(20, 3, 14, 30),
            [
                new CableSetup("vlek-1", "Vlek 1", null, 2, 180, [new ObstacleSetup("Kicker", "kicker", 40, null)]),
                new CableSetup("vlek-2", "Vlek 2", null, 2, null, null),
            ]);

        OperationsSetupResult first = await handler.HandleAsync(new ApplyOperationsSetupCommand(ParkId, setup), TestContext.Current.CancellationToken);
        OperationsSetupResult second = await handler.HandleAsync(
            new ApplyOperationsSetupCommand(ParkId, setup with { Cables = [setup.Cables[0] with { Name = "Velký vlek" }] }),
            TestContext.Current.CancellationToken);

        first.ShouldBe(new OperationsSetupResult(2, 0, 0));
        second.ShouldBe(new OperationsSetupResult(0, 1, 1));
        _repository.Cables.Single(c => c.Code == "vlek-1").Name.ShouldBe("Velký vlek");
        _repository.Cables.Single(c => c.Code == "vlek-1").Obstacles.Single().PositionMeters.ShouldBe(40);
        _repository.Cables.Single(c => c.Code == "vlek-2").IsActive.ShouldBeFalse();
        _repository.Settings.Single().Capacity.ShouldBe(3);
    }

    private ParkAccess Access() => new(_parks, _tenant, _clock);

    private Task<OperatingDaysImportDto> ApplyAsync(string csv) =>
        new ApplyOperatingDaysImportCommandHandler(_repository, _unitOfWork, Access())
            .HandleAsync(new ApplyOperatingDaysImportCommand(ParkId, csv), TestContext.Current.CancellationToken);

    private Task<OperatingDaysImportDto> PreviewAsync(string csv) =>
        new PreviewOperatingDaysImportQueryHandler(_repository, Access())
            .HandleAsync(new PreviewOperatingDaysImportQuery(ParkId, csv), TestContext.Current.CancellationToken);

    private Task<BlockingDto> CreateBlockingAsync(CreateBlockingRequest request) =>
        new CreateBlockingCommandHandler(_repository, _unitOfWork, Access())
            .HandleAsync(new CreateBlockingCommand(ParkId, request), TestContext.Current.CancellationToken);

    /// <summary>Repozitář v paměti — „uloží“ tím, že objekty drží.</summary>
    private sealed class InMemoryOperationsRepository : IOperationsRepository
    {
        public List<Cable> Cables { get; } = [];

        public List<ParkSettings> Settings { get; } = [];

        public List<OperatingDay> Days { get; } = [];

        public List<Blocking> Blockings { get; } = [];

        public Task<Cable?> FindCableAsync(Guid cableId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Cables.Find(c => c.Id == cableId));

        public Task<IReadOnlyList<Cable>> GetCablesAsync(Guid wakeParkId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Cable>>([.. Cables.Where(c => c.WakeParkId == wakeParkId)]);

        public Task<ParkSettings?> GetSettingsAsync(Guid wakeParkId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings.Find(s => s.WakeParkId == wakeParkId));

        public Task<IReadOnlyList<OperatingDay>> GetOperatingDaysAsync(Guid wakeParkId, DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OperatingDay>>([.. Days.Where(d => d.WakeParkId == wakeParkId && d.Date >= firstDay && d.Date <= lastDay).OrderBy(d => d.Date)]);

        public Task<IReadOnlyList<Blocking>> GetBlockingsAsync(Guid wakeParkId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Blocking>>([.. Blockings.Where(b => b.WakeParkId == wakeParkId && b.Overlaps(fromUtc, toUtc))]);

        public Task<Blocking?> FindBlockingAsync(Guid blockingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Blockings.Find(b => b.Id == blockingId));

        public void Add(Cable cable) => Cables.Add(cable);

        public void Add(ParkSettings settings) => Settings.Add(settings);

        public void Add(OperatingDay day) => Days.Add(day);

        public void Add(Blocking blocking) => Blockings.Add(blocking);

        public void Remove(OperatingDay day) => Days.Remove(day);

        public void Remove(Blocking blocking) => Blockings.Remove(blocking);
    }
}
