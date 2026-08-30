using Microsoft.EntityFrameworkCore;

using Shouldly;

using WakePark.Modules.Reservations.Domain.Slots;
using WakePark.Modules.Reservations.Infrastructure.Persistence;

namespace WakePark.Modules.Reservations.IntegrationTests;

/// <summary>
/// Kapacita slotu proti reálné databázi. Optimistický zámek přes <c>xmin</c> ani unikátní index
/// se v paměti otestovat nedají.
/// </summary>
public sealed class SlotConcurrencyTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Soubezna_zmena_stejneho_slotu_skonci_konfliktem_ne_prepsanim()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid cableId = Guid.CreateVersion7();
        Guid slotId;

        await using (ReservationsDbContext db = postgres.CreateDbContext())
        {
            Slot slot = Slot.Materialize(cableId, Start, 20, 3);
            db.Slots.Add(slot);
            await db.SaveChangesAsync(token);
            slotId = slot.Id;
        }

        // Dvě nezávislé jednotky práce načtou tentýž slot a obě do něj chtějí rezervovat.
        await using ReservationsDbContext first = postgres.CreateDbContext();
        await using ReservationsDbContext second = postgres.CreateDbContext();

        Slot firstSlot = await first.Slots.FirstAsync(s => s.Id == slotId, token);
        Slot secondSlot = await second.Slots.FirstAsync(s => s.Id == slotId, token);

        firstSlot.Reserve(2);
        await first.SaveChangesAsync(token);

        secondSlot.Reserve(2);

        // Druhá musí selhat na optimistickém zámku, ne tiše přepsat obsazenost té první.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(token));

        await using ReservationsDbContext verify = postgres.CreateDbContext();
        Slot stored = await verify.Slots.FirstAsync(s => s.Id == slotId, token);
        stored.ReservedCount.ShouldBe(2);
    }

    [Fact]
    public async Task Dva_sloty_na_stejny_cas_a_vlek_databaze_nepripusti()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid cableId = Guid.CreateVersion7();
        DateTimeOffset start = Start.AddHours(1);

        await using (ReservationsDbContext db = postgres.CreateDbContext())
        {
            db.Slots.Add(Slot.Materialize(cableId, start, 20, 3));
            await db.SaveChangesAsync(token);
        }

        await using ReservationsDbContext duplicate = postgres.CreateDbContext();
        duplicate.Slots.Add(Slot.Materialize(cableId, start, 20, 3));

        await Should.ThrowAsync<DbUpdateException>(() => duplicate.SaveChangesAsync(token));
    }
}
