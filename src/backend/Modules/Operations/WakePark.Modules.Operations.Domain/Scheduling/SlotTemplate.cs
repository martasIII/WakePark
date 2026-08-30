namespace WakePark.Modules.Operations.Domain.Scheduling;

/// <summary>
/// Vypočtený předpis jednoho slotu. Neukládá se — vzniká z provozní doby při každém dotazu.
/// </summary>
/// <param name="StartUtc">Začátek bloku jako okamžik.</param>
/// <param name="DurationMinutes">Délka bloku v minutách.</param>
/// <param name="Capacity">Kolik jezdců se v bloku vystřídá.</param>
public sealed record SlotTemplate(DateTimeOffset StartUtc, int DurationMinutes, int Capacity);
