using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Parks;

namespace WakePark.Modules.Operations.Application.Setup;

/// <summary>Nastavení areálu a vleky z výchozích dat (<c>provoz.json</c>).</summary>
public sealed record OperationsSetup(ParkSettingsSetup Settings, IReadOnlyList<CableSetup> Cables);

public sealed record ParkSettingsSetup(int SlotDurationMinutes, int Capacity, int PublicHoursDaysAhead, int PublicBookingDaysAhead);

/// <summary>Vlek. <see cref="Code"/> je stálý — podle něj se páruje s databází.</summary>
public sealed record CableSetup(
    string Code,
    string Name,
    string? Description,
    int TowerCount,
    int? LengthMeters,
    IReadOnlyList<ObstacleSetup>? Obstacles);

public sealed record ObstacleSetup(string Name, string Kind, int? PositionMeters, string? Description);

/// <summary>
/// Srovná nastavení areálu a vleky s výchozími daty (ADR 0009, upřesnění 24. 10.). Dokud vleky
/// nejdou upravit v backoffice, je zdrojem pravdy soubor: co v něm je, se založí nebo přepíše,
/// vlek, který ze souboru zmizel, se vyřadí (nemaže se — odkazují na něj rezervace).
/// </summary>
public sealed record ApplyOperationsSetupCommand(Guid WakeParkId, OperationsSetup Setup) : ICommand<OperationsSetupResult>;

public sealed record OperationsSetupResult(int CablesCreated, int CablesUpdated, int CablesDeactivated)
{
    public override string ToString() =>
        $"vleky: nové {CablesCreated}, aktualizované {CablesUpdated}, vyřazené {CablesDeactivated}";
}

internal sealed class ApplyOperationsSetupCommandHandler(IOperationsRepository repository, IOperationsUnitOfWork unitOfWork)
    : ICommandHandler<ApplyOperationsSetupCommand, OperationsSetupResult>
{
    public async Task<OperationsSetupResult> HandleAsync(ApplyOperationsSetupCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        OperationsSetup setup = command.Setup;
        ParkSettingsSetup s = setup.Settings;

        string? duplicate = setup.Cables.GroupBy(c => c.Code, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new DomainException("setup.duplicate_cable", $"Vlek s kódem '{duplicate}' je v souboru víckrát.");
        }

        ParkSettings? settings = await repository.GetSettingsAsync(command.WakeParkId, cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            repository.Add(ParkSettings.Create(command.WakeParkId, s.SlotDurationMinutes, s.Capacity, s.PublicHoursDaysAhead, s.PublicBookingDaysAhead));
        }
        else
        {
            settings.Update(s.SlotDurationMinutes, s.Capacity, s.PublicHoursDaysAhead, s.PublicBookingDaysAhead);
        }

        Dictionary<string, Cable> existing = (await repository.GetCablesAsync(command.WakeParkId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(c => c.Code, StringComparer.Ordinal);

        int created = 0, updated = 0, deactivated = 0;

        foreach (CableSetup item in setup.Cables)
        {
            if (existing.Remove(item.Code, out Cable? cable))
            {
                updated++;
            }
            else
            {
                cable = Cable.Create(command.WakeParkId, item.Code, item.Name, item.TowerCount);
                repository.Add(cable);
                created++;
            }

            cable.Update(
                item.Name,
                item.TowerCount,
                item.LengthMeters,
                item.Description,
                [.. (item.Obstacles ?? []).Select(o => new Obstacle(o.Name, o.Kind, o.PositionMeters, o.Description))]);
            cable.Activate();
        }

        foreach (Cable missing in existing.Values.Where(c => c.IsActive))
        {
            missing.Deactivate();
            deactivated++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new OperationsSetupResult(created, updated, deactivated);
    }
}
