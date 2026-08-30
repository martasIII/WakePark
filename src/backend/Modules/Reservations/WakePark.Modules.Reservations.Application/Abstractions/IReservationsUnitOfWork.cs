using WakePark.BuildingBlocks.Application.Abstractions;

namespace WakePark.Modules.Reservations.Application.Abstractions;

/// <summary>
/// Potvrzení změn modulu Reservations.
/// </summary>
/// <remarks>
/// Vlastní rozhraní pro každý modul je nutnost, ne ozdoba: všechny moduly běží v jednom
/// DI kontejneru, takže sdílený <see cref="IUnitOfWork"/> by se přepsal registrací posledního
/// modulu a handler by ukládal cizí kontext.
/// </remarks>
public interface IReservationsUnitOfWork : IUnitOfWork;
