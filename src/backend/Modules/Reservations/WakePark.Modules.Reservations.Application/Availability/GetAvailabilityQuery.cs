using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Reservations.Application.Abstractions;
using WakePark.Modules.Reservations.Contracts;
using WakePark.Modules.Reservations.Domain.Slots;

namespace WakePark.Modules.Reservations.Application.Availability;

/// <summary>Volná kapacita vleku pro jeden den v lokálním čase areálu.</summary>
public sealed record GetAvailabilityQuery(Guid CableId, DateOnly LocalDate)
    : IQuery<IReadOnlyList<SlotAvailabilityDto>>;

/// <summary>
/// Spojí rozvrh (co by se dalo jezdit) s materializovanými sloty (co je obsazené).
/// Bloky bez jediné rezervace v databázi vůbec neexistují, proto se doplní jako plně volné.
/// </summary>
internal sealed class GetAvailabilityQueryHandler(
    IOperationsSchedule schedule,
    IReservationsRepository repository)
    : IQueryHandler<GetAvailabilityQuery, IReadOnlyList<SlotAvailabilityDto>>
{
    public async Task<IReadOnlyList<SlotAvailabilityDto>> HandleAsync(
        GetAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<SlotTemplateDto> templates = await schedule
            .GetSlotTemplatesAsync(query.CableId, query.LocalDate, cancellationToken)
            .ConfigureAwait(false);

        if (templates.Count == 0)
        {
            return [];
        }

        DateTimeOffset fromUtc = templates[0].StartUtc;
        SlotTemplateDto last = templates[^1];
        DateTimeOffset toUtc = last.StartUtc.AddMinutes(last.DurationMinutes);

        IReadOnlyList<Slot> materialized = await repository
            .GetSlotsAsync(query.CableId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<DateTimeOffset, Slot> byStart = materialized.ToDictionary(slot => slot.StartUtc);

        return
        [
            .. templates.Select(template => byStart.TryGetValue(template.StartUtc, out Slot? slot)
                ? new SlotAvailabilityDto(
                    slot.StartUtc,
                    slot.EndUtc,
                    slot.Capacity,
                    slot.ReservedCount,
                    slot.Status is SlotStatus.Open)
                : new SlotAvailabilityDto(
                    template.StartUtc,
                    template.StartUtc.AddMinutes(template.DurationMinutes),
                    template.Capacity,
                    ReservedCount: 0,
                    IsOpen: true))
        ];
    }
}
