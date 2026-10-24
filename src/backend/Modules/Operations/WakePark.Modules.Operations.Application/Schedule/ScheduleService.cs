using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Application.Calendar;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Calendar;
using WakePark.Modules.Operations.Domain.Parks;

namespace WakePark.Modules.Operations.Application.Schedule;

/// <summary>
/// Poskládá rozvrh vleku (ADR 0009): provozní den areálu → bloky podle nastavení areálu →
/// bez blokací vleku nebo areálu. Implementuje veřejný kontrakt, přes který si rozvrh čte
/// modul Reservations.
/// </summary>
internal sealed class ScheduleService(
    IOperationsRepository repository,
    IParkTimeZoneProvider timeZoneProvider) : IOperationsSchedule
{
    public async Task<IReadOnlyList<SlotTemplateDto>> GetSlotTemplatesAsync(
        Guid cableId,
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        Cable cable = await repository.FindCableAsync(cableId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("cable.not_found", "Vlek neexistuje.");

        if (!cable.IsActive)
        {
            return [];
        }

        // Areál bez nastavení ještě není připravený k provozu — nejezdí se.
        ParkSettings? settings = await repository.GetSettingsAsync(cable.WakeParkId, cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            return [];
        }

        IReadOnlyList<OperatingDay> days = await repository
            .GetOperatingDaysAsync(cable.WakeParkId, localDate, localDate, cancellationToken)
            .ConfigureAwait(false);

        if (days.Count == 0)
        {
            return [];
        }

        TimeZoneInfo timeZone = await timeZoneProvider
            .GetTimeZoneAsync(cable.WakeParkId, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset dayStart = ParkAccess.StartOfDayUtc(localDate, timeZone);
        DateTimeOffset dayEnd = ParkAccess.StartOfDayUtc(localDate.AddDays(1), timeZone);

        IReadOnlyList<Blocking> blockings = await repository
            .GetBlockingsAsync(cable.WakeParkId, dayStart, dayEnd, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. DayPlanner
                .Plan(cableId, days[0], settings, timeZone, blockings)
                .Select(slot => new SlotTemplateDto(cableId, slot.StartUtc, slot.DurationMinutes, slot.Capacity)),
        ];
    }

    public async Task<SlotTemplateDto?> FindSlotTemplateAsync(
        Guid cableId,
        DateTimeOffset startUtc,
        CancellationToken cancellationToken = default)
    {
        Cable cable = await repository.FindCableAsync(cableId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("cable.not_found", "Vlek neexistuje.");

        TimeZoneInfo timeZone = await timeZoneProvider
            .GetTimeZoneAsync(cable.WakeParkId, cancellationToken)
            .ConfigureAwait(false);

        // Okamžik může v lokálním čase spadat do jiného dne, než v jakém je v UTC.
        // Prohledáme proto i sousední dny, jinak by slot na hraně půlnoci "zmizel".
        DateOnly localDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(startUtc, timeZone).DateTime);

        foreach (int dayOffset in (int[])[0, -1, 1])
        {
            IReadOnlyList<SlotTemplateDto> templates = await GetSlotTemplatesAsync(
                cableId,
                localDate.AddDays(dayOffset),
                cancellationToken).ConfigureAwait(false);

            SlotTemplateDto? match = templates.FirstOrDefault(template => template.StartUtc == startUtc);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
