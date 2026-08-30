using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Scheduling;
using WakePark.Modules.Operations.Domain.Seasons;

namespace WakePark.Modules.Operations.Application.Schedule;

/// <summary>
/// Poskládá rozvrh vleku: sezóna → provozní doba pro den v týdnu → výjimky → sloty.
/// Implementuje veřejný kontrakt, přes který si rozvrh čte modul Reservations.
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

        Season? season = await repository
            .FindSeasonAsync(cableId, localDate, cancellationToken)
            .ConfigureAwait(false);

        if (season is null)
        {
            // Mimo sezónu se nejezdí. Není to chyba, jen prázdný rozvrh.
            return [];
        }

        IReadOnlyList<WorkingHours> workingHours = await repository
            .GetWorkingHoursAsync(cableId, season.Id, localDate.DayOfWeek, cancellationToken)
            .ConfigureAwait(false);

        if (workingHours.Count == 0)
        {
            return [];
        }

        IReadOnlyList<DailySchedule> schedules = [.. workingHours.Select(hours => hours.ToDailySchedule())];

        ScheduleOverride? exception = await repository
            .FindScheduleOverrideAsync(cableId, localDate, cancellationToken)
            .ConfigureAwait(false);

        if (exception is not null)
        {
            schedules = exception.ApplyTo(schedules);
        }

        if (schedules.Count == 0)
        {
            return [];
        }

        TimeZoneInfo timeZone = await timeZoneProvider
            .GetTimeZoneAsync(cable.WakeParkId, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. SlotPlanner
                .Plan(localDate, timeZone, schedules)
                .Select(slot => new SlotTemplateDto(cableId, slot.StartUtc, slot.DurationMinutes, slot.Capacity))
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
