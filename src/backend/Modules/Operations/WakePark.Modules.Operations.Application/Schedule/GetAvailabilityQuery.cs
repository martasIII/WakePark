using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Operations.Contracts;

namespace WakePark.Modules.Operations.Application.Schedule;

/// <summary>Rozvrh vleku pro jeden den v lokálním čase areálu.</summary>
public sealed record GetScheduleQuery(Guid CableId, DateOnly LocalDate)
    : IQuery<IReadOnlyList<SlotTemplateDto>>;

internal sealed class GetScheduleQueryHandler(IOperationsSchedule schedule)
    : IQueryHandler<GetScheduleQuery, IReadOnlyList<SlotTemplateDto>>
{
    public Task<IReadOnlyList<SlotTemplateDto>> HandleAsync(
        GetScheduleQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return schedule.GetSlotTemplatesAsync(query.CableId, query.LocalDate, cancellationToken);
    }
}
