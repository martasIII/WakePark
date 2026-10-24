using WakePark.BuildingBlocks.Application;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Calendar;
using WakePark.Modules.Operations.Domain.Parks;
using WakePark.Modules.Operations.Domain.Scheduling;

namespace WakePark.Modules.Operations.Application.Calendar;

// Provozní dny, blokace a vleky (ADR 0009).

/// <summary>Otevírací doba na webu: od dneška tolik dní, kolik má areál nastaveno. Veřejné.</summary>
public sealed record GetPublicOpeningHoursQuery(Guid WakeParkId) : IQuery<PublicOpeningHoursDto>;

/// <summary>Provozní kalendář pro backoffice v rozsahu dat (nejvýš rok).</summary>
public sealed record GetOperatingCalendarQuery(Guid WakeParkId, DateOnly From, DateOnly To) : IQuery<OperatingCalendarDto>;

/// <summary>Otevírací doba jednoho dne, nebo zavřeno.</summary>
public sealed record SetOperatingDayCommand(Guid WakeParkId, DateOnly Date, SetOperatingDayRequest Request) : ICommand<OperatingCalendarDto>;

/// <summary>Náhled importu CSV — nic neuloží.</summary>
public sealed record PreviewOperatingDaysImportQuery(Guid WakeParkId, string Csv) : IQuery<OperatingDaysImportDto>;

/// <summary>Import CSV. S chybou v souboru neuloží nic.</summary>
public sealed record ApplyOperatingDaysImportCommand(Guid WakeParkId, string Csv) : ICommand<OperatingDaysImportDto>;

public sealed record CreateBlockingCommand(Guid WakeParkId, CreateBlockingRequest Request) : ICommand<BlockingDto>;

public sealed record DeleteBlockingCommand(Guid WakeParkId, Guid BlockingId) : ICommand<bool>;

/// <summary>Vleky areálu s parametry a překážkami.</summary>
public sealed record GetCablesQuery(Guid WakeParkId) : IQuery<IReadOnlyList<CableDetailDto>>;

internal sealed class GetPublicOpeningHoursQueryHandler(IOperationsRepository repository, ParkAccess access)
    : IQueryHandler<GetPublicOpeningHoursQuery, PublicOpeningHoursDto>
{
    public async Task<PublicOpeningHoursDto> HandleAsync(GetPublicOpeningHoursQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ParkInfo park = await access.PublicAsync(query.WakeParkId, cancellationToken).ConfigureAwait(false);
        ParkSettings? settings = await repository.GetSettingsAsync(park.Id, cancellationToken).ConfigureAwait(false);

        DateOnly from = access.Today(park);
        DateOnly to = from.AddDays((settings?.PublicHoursDaysAhead ?? 14) - 1);

        Dictionary<DateOnly, OperatingDay> days = (await repository
                .GetOperatingDaysAsync(park.Id, from, to, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(d => d.Date);

        IReadOnlyList<Blocking> blockings = await repository
            .GetBlockingsAsync(park.Id, ParkAccess.StartOfDayUtc(from, park.TimeZone), ParkAccess.StartOfDayUtc(to.AddDays(1), park.TimeZone), cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, string> cableNames = (await repository.GetCablesAsync(park.Id, cancellationToken).ConfigureAwait(false))
            .ToDictionary(c => c.Id, c => c.Name);

        List<PublicOpeningDayDto> result = [];
        for (DateOnly date = from; date <= to; date = date.AddDays(1))
        {
            if (!days.TryGetValue(date, out OperatingDay? day))
            {
                result.Add(new PublicOpeningDayDto(date, null, null, []));
                continue;
            }

            result.Add(new PublicOpeningDayDto(date, day.OpensAt, day.ClosesAt, [.. PublicBlockings(day, blockings, cableNames, park.TimeZone)]));
        }

        return new PublicOpeningHoursDto(park.TimeZone.Id, result);
    }

    /// <summary>
    /// Blokace ořezané na otevírací dobu dne, jen s důvodem — poznámka je interní.
    /// Blokace mimo otevírací dobu web nezajímá.
    /// </summary>
    private static IEnumerable<PublicBlockingDto> PublicBlockings(
        OperatingDay day,
        IReadOnlyList<Blocking> blockings,
        Dictionary<Guid, string> cableNames,
        TimeZoneInfo timeZone)
    {
        foreach (Blocking blocking in blockings.OrderBy(b => b.StartUtc))
        {
            DateTime start = TimeZoneInfo.ConvertTime(blocking.StartUtc, timeZone).DateTime;
            DateTime end = TimeZoneInfo.ConvertTime(blocking.EndUtc, timeZone).DateTime;
            DateTime opens = day.Date.ToDateTime(day.OpensAt);
            DateTime closes = day.Date.ToDateTime(day.ClosesAt);

            DateTime from = start > opens ? start : opens;
            DateTime to = end < closes ? end : closes;

            if (from < to)
            {
                yield return new PublicBlockingDto(
                    blocking.CableId is { } cableId ? cableNames.GetValueOrDefault(cableId) : null,
                    TimeOnly.FromDateTime(from),
                    TimeOnly.FromDateTime(to),
                    blocking.Category.ToString());
            }
        }
    }
}

internal sealed class GetOperatingCalendarQueryHandler(IOperationsRepository repository, ParkAccess access)
    : IQueryHandler<GetOperatingCalendarQuery, OperatingCalendarDto>
{
    public const int MaxDays = 400;

    public async Task<OperatingCalendarDto> HandleAsync(GetOperatingCalendarQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.To < query.From || query.To.DayNumber - query.From.DayNumber >= MaxDays)
        {
            throw new DomainException("calendar.invalid_range", $"Rozsah musí být od–do, nejvýš {MaxDays} dní.");
        }

        ParkInfo park = await access.ForTenantAsync(query.WakeParkId, cancellationToken).ConfigureAwait(false);
        return await CalendarView.LoadAsync(repository, park, query.From, query.To, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class SetOperatingDayCommandHandler(IOperationsRepository repository, IOperationsUnitOfWork unitOfWork, ParkAccess access)
    : ICommandHandler<SetOperatingDayCommand, OperatingCalendarDto>
{
    public async Task<OperatingCalendarDto> HandleAsync(SetOperatingDayCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        SetOperatingDayRequest request = command.Request;

        ParkInfo park = await access.ForTenantAsync(command.WakeParkId, cancellationToken).ConfigureAwait(false);
        OperatingDay? day = (await repository.GetOperatingDaysAsync(park.Id, command.Date, command.Date, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();

        switch ((request.OpensAt, request.ClosesAt))
        {
            case (null, null):
                if (day is not null)
                {
                    repository.Remove(day);
                }

                break;

            case ({ } opensAt, { } closesAt):
                if (day is null)
                {
                    repository.Add(OperatingDay.Create(park.Id, command.Date, opensAt, closesAt));
                }
                else
                {
                    day.SetHours(opensAt, closesAt);
                }

                break;

            default:
                throw new DomainException("operating_day.incomplete", "Vyplňte otevírání i zavírání, nebo nic (= zavřeno).");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await CalendarView.LoadAsync(repository, park, command.Date, command.Date, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class PreviewOperatingDaysImportQueryHandler(IOperationsRepository repository, ParkAccess access)
    : IQueryHandler<PreviewOperatingDaysImportQuery, OperatingDaysImportDto>
{
    public async Task<OperatingDaysImportDto> HandleAsync(PreviewOperatingDaysImportQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ParkInfo park = await access.ForTenantAsync(query.WakeParkId, cancellationToken).ConfigureAwait(false);
        (CsvParseResult csv, IReadOnlyList<OperatingDayDiff> plan, _) = await OperatingDaysImportPlanner
            .PlanAsync(repository, park, query.Csv, cancellationToken)
            .ConfigureAwait(false);

        return OperatingDaysImportPlanner.ToDto(csv, plan);
    }
}

internal sealed class ApplyOperatingDaysImportCommandHandler(IOperationsRepository repository, IOperationsUnitOfWork unitOfWork, ParkAccess access)
    : ICommandHandler<ApplyOperatingDaysImportCommand, OperatingDaysImportDto>
{
    public async Task<OperatingDaysImportDto> HandleAsync(ApplyOperatingDaysImportCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ParkInfo park = await access.ForTenantAsync(command.WakeParkId, cancellationToken).ConfigureAwait(false);
        (CsvParseResult csv, IReadOnlyList<OperatingDayDiff> plan, IReadOnlyList<OperatingDay> existing) = await OperatingDaysImportPlanner
            .PlanAsync(repository, park, command.Csv, cancellationToken)
            .ConfigureAwait(false);

        // Napůl nahraná sezóna by byla horší než žádná.
        if (!csv.IsValid)
        {
            throw new DomainException("operating_days.invalid_csv", string.Join(" ", csv.Errors));
        }

        Dictionary<DateOnly, OperatingDay> byDate = existing.ToDictionary(d => d.Date);

        foreach (OperatingDayDiff diff in plan)
        {
            switch (diff.Change)
            {
                case OperatingDayChange.Added:
                    repository.Add(OperatingDay.Create(park.Id, diff.Date, diff.NewOpensAt!.Value, diff.NewClosesAt!.Value));
                    break;
                case OperatingDayChange.Changed:
                    byDate[diff.Date].SetHours(diff.NewOpensAt!.Value, diff.NewClosesAt!.Value);
                    break;
                case OperatingDayChange.Closed:
                    repository.Remove(byDate[diff.Date]);
                    break;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OperatingDaysImportPlanner.ToDto(csv, plan);
    }
}

internal sealed class CreateBlockingCommandHandler(IOperationsRepository repository, IOperationsUnitOfWork unitOfWork, ParkAccess access)
    : ICommandHandler<CreateBlockingCommand, BlockingDto>
{
    public async Task<BlockingDto> HandleAsync(CreateBlockingCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        CreateBlockingRequest request = command.Request;

        ParkInfo park = await access.ForTenantAsync(command.WakeParkId, cancellationToken).ConfigureAwait(false);

        if (request.CableId is { } cableId
            && (await repository.FindCableAsync(cableId, cancellationToken).ConfigureAwait(false))?.WakeParkId != park.Id)
        {
            throw new DomainException("blocking.cable_not_found", "Vlek v tomto areálu není.");
        }

        if (!Enum.TryParse(request.Category, ignoreCase: true, out BlockingCategory category) || !Enum.IsDefined(category))
        {
            throw new DomainException("blocking.invalid_category", "Neznámý důvod blokace (TechnicalFault, PrivateEvent, Weather, Other).");
        }

        Blocking blocking = Blocking.Create(
            park.Id, request.CableId, ToUtc(request.Start, park.TimeZone), ToUtc(request.End, park.TimeZone), category, request.Note);

        repository.Add(blocking);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CalendarView.ToDto(blocking);
    }

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo timeZone) =>
        SlotPlanner.TryToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone, out DateTimeOffset utc)
            ? utc
            : throw new DomainException("blocking.invalid_time", $"Čas {local:d. M. yyyy H:mm} kvůli přechodu na letní čas neexistuje.");
}

internal sealed class DeleteBlockingCommandHandler(IOperationsRepository repository, IOperationsUnitOfWork unitOfWork, ParkAccess access)
    : ICommandHandler<DeleteBlockingCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteBlockingCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ParkInfo park = await access.ForTenantAsync(command.WakeParkId, cancellationToken).ConfigureAwait(false);
        Blocking? blocking = await repository.FindBlockingAsync(command.BlockingId, cancellationToken).ConfigureAwait(false);

        if (blocking is null || blocking.WakeParkId != park.Id)
        {
            throw new NotFoundException("Blokace nenalezena.");
        }

        repository.Remove(blocking);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

internal sealed class GetCablesQueryHandler(IOperationsRepository repository, ParkAccess access)
    : IQueryHandler<GetCablesQuery, IReadOnlyList<CableDetailDto>>
{
    public async Task<IReadOnlyList<CableDetailDto>> HandleAsync(GetCablesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ParkInfo park = await access.ForTenantAsync(query.WakeParkId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Cable> cables = await repository.GetCablesAsync(park.Id, cancellationToken).ConfigureAwait(false);

        return [.. cables.Select(CalendarView.ToDto)];
    }
}

/// <summary>Společné skládání odpovědí kalendáře.</summary>
internal static class CalendarView
{
    public static async Task<OperatingCalendarDto> LoadAsync(
        IOperationsRepository repository,
        ParkInfo park,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OperatingDay> days = await repository.GetOperatingDaysAsync(park.Id, from, to, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Blocking> blockings = await repository
            .GetBlockingsAsync(park.Id, ParkAccess.StartOfDayUtc(from, park.TimeZone), ParkAccess.StartOfDayUtc(to.AddDays(1), park.TimeZone), cancellationToken)
            .ConfigureAwait(false);

        return new OperatingCalendarDto(
            park.TimeZone.Id,
            from,
            to,
            [.. days.OrderBy(d => d.Date).Select(d => new OperatingDayDto(d.Date, d.OpensAt, d.ClosesAt))],
            [.. blockings.OrderBy(b => b.StartUtc).Select(ToDto)]);
    }

    public static BlockingDto ToDto(Blocking blocking) => new(
        blocking.Id,
        blocking.CableId,
        blocking.StartUtc,
        blocking.EndUtc,
        blocking.Category.ToString(),
        blocking.Note,
        blocking.CreatedBy,
        blocking.CreatedAtUtc);

    public static CableDetailDto ToDto(Cable cable) => new(
        cable.Id,
        cable.Code,
        cable.Name,
        cable.Description,
        cable.TowerCount,
        cable.LengthMeters,
        [.. cable.Obstacles.Select(o => new ObstacleDto(o.Name, o.Kind, o.PositionMeters, o.Description))],
        cable.IsActive);
}

/// <summary>Náhled a import CSV používají stejný plán.</summary>
internal static class OperatingDaysImportPlanner
{
    public static async Task<(CsvParseResult Csv, IReadOnlyList<OperatingDayDiff> Plan, IReadOnlyList<OperatingDay> Existing)> PlanAsync(
        IOperationsRepository repository,
        ParkInfo park,
        string csvText,
        CancellationToken cancellationToken)
    {
        CsvParseResult csv = OperatingDaysCsv.Parse(csvText ?? string.Empty);

        if (csv.Days.Count == 0)
        {
            return (csv, [], []);
        }

        IReadOnlyList<OperatingDay> existing = await repository
            .GetOperatingDaysAsync(park.Id, csv.Days[0].Date, csv.Days[^1].Date, cancellationToken)
            .ConfigureAwait(false);

        return (csv, OperatingDaysImport.Plan(existing, csv.Days), existing);
    }

    public static OperatingDaysImportDto ToDto(CsvParseResult csv, IReadOnlyList<OperatingDayDiff> plan) => new(
        csv.Errors,
        [.. plan.Select(d => new OperatingDayDiffDto(d.Date, d.Change.ToString(), d.OldOpensAt, d.OldClosesAt, d.NewOpensAt, d.NewClosesAt))],
        plan.Count(d => d.Change == OperatingDayChange.Added),
        plan.Count(d => d.Change == OperatingDayChange.Changed),
        plan.Count(d => d.Change == OperatingDayChange.Closed),
        plan.Count(d => d.Change == OperatingDayChange.Unchanged));
}
