namespace WakePark.Modules.Reservations.Application.Abstractions;

/// <summary>
/// Zjistí, kterému areálu vlek patří. Vlek vlastní modul Operations, areál modul Tenancy —
/// tohle je jediné místo, kde se ty dva pohledy spojují.
/// </summary>
public interface ICableLocator
{
    Task<Guid?> FindWakeParkIdAsync(Guid cableId, CancellationToken cancellationToken = default);
}
