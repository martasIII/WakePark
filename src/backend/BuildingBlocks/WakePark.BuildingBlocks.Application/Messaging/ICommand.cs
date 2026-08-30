namespace WakePark.BuildingBlocks.Application.Messaging;

/// <summary>Požadavek, který mění stav systému.</summary>
/// <typeparam name="TResponse">Typ výsledku operace.</typeparam>
public interface ICommand<TResponse>;

/// <summary>Obsluha příkazu. Registruje se do DI, dispatch jde přes kontejner, ne přes mediator knihovnu.</summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
