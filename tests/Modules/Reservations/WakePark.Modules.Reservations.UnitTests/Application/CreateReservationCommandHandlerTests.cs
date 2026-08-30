using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Reservations.Application.Abstractions;
using WakePark.Modules.Reservations.Application.Reservations.CreateReservation;
using WakePark.Modules.Reservations.Contracts;
using WakePark.Modules.Reservations.Domain.Passes;
using WakePark.Modules.Reservations.Domain.Reservations;
using WakePark.Modules.Reservations.Domain.Slots;

namespace WakePark.Modules.Reservations.UnitTests.Application;

/// <summary>
/// Odečet kreditu se děje v handleru, ne v entitě — proto se pravidlo „jedno místo = jedna jízda"
/// hlídá právě tady. Doménové testy by ho neodhalily.
/// </summary>
public sealed class CreateReservationCommandHandlerTests
{
    private static readonly Guid CableId = Guid.CreateVersion7();

    private static readonly Guid CustomerId = Guid.CreateVersion7();

    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SlotStart = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly IReservationsRepository _repository = Substitute.For<IReservationsRepository>();
    private readonly IOperationsSchedule _schedule = Substitute.For<IOperationsSchedule>();
    private readonly IReservationsUnitOfWork _unitOfWork = Substitute.For<IReservationsUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public CreateReservationCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);

        _schedule
            .FindSlotTemplateAsync(CableId, SlotStart, Arg.Any<CancellationToken>())
            .Returns(new SlotTemplateDto(CableId, SlotStart, DurationMinutes: 20, Capacity: 3));

        _repository
            .FindSlotAsync(CableId, SlotStart, Arg.Any<CancellationToken>())
            .Returns((Slot?)null);
    }

    private CreateReservationCommandHandler CreateHandler() =>
        new(_repository, _schedule, _unitOfWork, _clock);

    private RidePass GivenPassWithCredit(int rides)
    {
        RidePass pass = RidePass.Create(
            CustomerId,
            Guid.CreateVersion7(),
            RidePassKind.RideCredit,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 9, 30));

        pass.TopUp(rides, Now);

        _repository.GetRidePassAsync(pass.Id, Arg.Any<CancellationToken>()).Returns(pass);

        return pass;
    }

    private static CreateReservationCommand Command(int seats, Guid? passId = null) =>
        new(CableId, SlotStart, CustomerId, seats, ReservationSource.Online, passId, IdempotencyKey: null);

    [Fact]
    public async Task Rodina_tri_osob_odecte_tri_jizdy()
    {
        RidePass pass = GivenPassWithCredit(10);

        await CreateHandler().HandleAsync(Command(seats: 3, pass.Id), TestContext.Current.CancellationToken);

        pass.Balance.ShouldBe(7);
    }

    [Fact]
    public async Task Rezervace_obsadi_tolik_mist_kolik_je_jezdcu()
    {
        RidePass pass = GivenPassWithCredit(10);
        Slot? captured = null;
        _repository.When(r => r.AddSlot(Arg.Any<Slot>())).Do(call => captured = call.Arg<Slot>());

        await CreateHandler().HandleAsync(Command(seats: 2, pass.Id), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.ReservedCount.ShouldBe(2);
        captured.FreeCapacity.ShouldBe(1);
    }

    [Fact]
    public async Task Slot_se_materializuje_az_s_prvni_rezervaci()
    {
        await CreateHandler().HandleAsync(Command(seats: 1), TestContext.Current.CancellationToken);

        _repository.Received(1).AddSlot(Arg.Is<Slot>(slot =>
            slot.CableId == CableId && slot.StartUtc == SlotStart && slot.Capacity == 3));
    }

    [Fact]
    public async Task Nedostatek_kreditu_rezervaci_nevytvori()
    {
        RidePass pass = GivenPassWithCredit(2);

        DomainException exception = await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(Command(seats: 3, pass.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("ride_pass.insufficient_credit");
#pragma warning disable xUnit1051 // U ověření na substitutu je token matcher, ne skutečná hodnota.
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task Cas_mimo_rozvrh_rezervaci_nevytvori()
    {
        _schedule
            .FindSlotTemplateAsync(CableId, SlotStart, Arg.Any<CancellationToken>())
            .Returns((SlotTemplateDto?)null);

        DomainException exception = await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(Command(seats: 1), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("slot.not_in_schedule");
    }

    [Fact]
    public async Task Rezervace_do_minulosti_neprojde()
    {
        _clock.UtcNow.Returns(SlotStart.AddMinutes(1));

        DomainException exception = await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(Command(seats: 1), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("reservation.in_past");
    }

    [Fact]
    public async Task Opakovane_odeslani_se_stejnym_klicem_vrati_puvodni_rezervaci()
    {
        Reservation original = Reservation.Create(
            Guid.CreateVersion7(), CustomerId, 2, ReservationSource.Online, "klic-1");

        _repository
            .FindByIdempotencyKeyAsync("klic-1", Arg.Any<CancellationToken>())
            .Returns(original);

        ReservationDto result = await CreateHandler().HandleAsync(
            new CreateReservationCommand(
                CableId, SlotStart, CustomerId, 2, ReservationSource.Online, null, "klic-1"),
            TestContext.Current.CancellationToken);

        result.Id.ShouldBe(original.Id);
        _repository.DidNotReceive().AddReservation(Arg.Any<Reservation>());
#pragma warning disable xUnit1051 // U ověření na substitutu je token matcher, ne skutečná hodnota.
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
#pragma warning restore xUnit1051
    }
}
