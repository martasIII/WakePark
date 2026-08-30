using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Reservations.Application.Abstractions;
using WakePark.Modules.Reservations.Contracts;
using WakePark.Modules.Reservations.Domain.Passes;
using WakePark.Modules.Reservations.Domain.Reservations;
using WakePark.Modules.Reservations.Domain.Slots;

namespace WakePark.Modules.Reservations.Application.Reservations.CreateReservation;

/// <summary>
/// Vytvoří rezervaci. Slot, obsazenost i odečet kreditu jsou ve stejném modulu, a tedy
/// ve <b>stejné databázové transakci</b> — proto tu nevzniká žádná saga ani kompenzace.
/// </summary>
internal sealed class CreateReservationCommandHandler(
    IReservationsRepository repository,
    IOperationsSchedule schedule,
    IReservationsUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CreateReservationCommand, ReservationDto>
{
    public async Task<ReservationDto> HandleAsync(
        CreateReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DateTimeOffset startUtc = command.StartUtc.ToUniversalTime();

        // 1) Opakované odeslání nesmí vytvořit druhou rezervaci.
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            Reservation? existing = await repository
                .FindByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                return ToDto(existing, startUtc);
            }
        }

        if (startUtc <= clock.UtcNow)
        {
            throw new DomainException("reservation.in_past", "Nelze rezervovat blok, který už začal.");
        }

        // 2) Slot musí odpovídat rozvrhu — nelze si vymyslet čas, kdy se nejezdí.
        SlotTemplateDto template = await schedule
            .FindSlotTemplateAsync(command.CableId, startUtc, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("slot.not_in_schedule", "V tento čas se na vleku nejezdí.");

        // 3) Slot se materializuje až teď, s první rezervací.
        Slot slot = await repository
            .FindSlotAsync(command.CableId, startUtc, cancellationToken)
            .ConfigureAwait(false)
            ?? MaterializeSlot(template);

        slot.Reserve(command.Seats);

        Reservation reservation = Reservation.Create(
            slot.Id,
            command.CustomerId,
            command.Seats,
            command.Source,
            string.IsNullOrWhiteSpace(command.IdempotencyKey) ? null : command.IdempotencyKey);

        // 4) Odečet kreditu ve stejné transakci jako obsazení slotu.
        if (command.RidePassId is { } ridePassId)
        {
            RidePass pass = await repository
                .GetRidePassAsync(ridePassId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new DomainException("ride_pass.not_found", "Permanentka neexistuje.");

            DateOnly slotDate = DateOnly.FromDateTime(startUtc.UtcDateTime);

            if (!pass.IsValidOn(slotDate))
            {
                throw new DomainException("ride_pass.not_valid", "Permanentka v termínu slotu neplatí.");
            }

            pass.Consume(command.Seats, reservation.Id, clock.UtcNow);
            reservation.ConfirmPaidByPass(ridePassId);
        }
        else
        {
            reservation.Confirm();
        }

        repository.AddReservation(reservation);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(reservation, slot.StartUtc);
    }

    private Slot MaterializeSlot(SlotTemplateDto template)
    {
        Slot slot = Slot.Materialize(
            template.CableId,
            template.StartUtc,
            template.DurationMinutes,
            template.Capacity);

        repository.AddSlot(slot);

        return slot;
    }

    private static ReservationDto ToDto(Reservation reservation, DateTimeOffset slotStartUtc) =>
        new(
            reservation.Id,
            reservation.SlotId,
            reservation.CustomerId,
            reservation.RidePassId,
            reservation.OrderId,
            slotStartUtc,
            reservation.Seats,
            reservation.Status.ToString(),
            reservation.Source.ToString(),
            reservation.CancelledAtUtc,
            reservation.RefundPercent);
}
