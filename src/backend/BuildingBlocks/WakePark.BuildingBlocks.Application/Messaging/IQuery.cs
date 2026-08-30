namespace WakePark.BuildingBlocks.Application.Messaging;

/// <summary>Požadavek, který jen čte. Nesmí měnit stav.</summary>
/// <typeparam name="TResponse">Typ výsledku dotazu.</typeparam>
public interface IQuery<TResponse>;

/// <summary>Obsluha dotazu.</summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
