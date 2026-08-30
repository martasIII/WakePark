using Microsoft.EntityFrameworkCore;

using Shouldly;

using WakePark.Modules.Reservations.Domain.Passes;
using WakePark.Modules.Reservations.Infrastructure.Persistence;

namespace WakePark.Modules.Reservations.IntegrationTests;

/// <summary>
/// Ukládání permanentky a jejích pohybů proti reálnému Postgresu.
/// </summary>
/// <remarks>
/// Tyhle testy existují kvůli konkrétní chybě: EF u <c>Guid</c> primárních klíčů předpokládá
/// generování na straně databáze, takže nový pohyb kreditu — který klíč dostal už v kódu —
/// považoval za existující řádek a místo INSERT vydal UPDATE. Ten nezměnil nic a skončil
/// výjimkou o souběhu. Jednotkové testy to odhalit nemohly, protože se to děje až v EF.
/// </remarks>
public sealed class RidePassPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    private static RidePass NewPass() => RidePass.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        RidePassKind.RideCredit,
        new DateOnly(2026, 5, 1),
        new DateOnly(2026, 9, 30));

    [Fact]
    public async Task Novy_pohyb_kreditu_se_ulozi_jako_vlozeni_ne_jako_aktualizace()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid passId;

        // Permanentka s nabitým kreditem.
        await using (ReservationsDbContext db = postgres.CreateDbContext())
        {
            RidePass pass = NewPass();
            pass.TopUp(10, Now);
            db.RidePasses.Add(pass);
            await db.SaveChangesAsync(token);
            passId = pass.Id;
        }

        // Čerpání v samostatné jednotce práce — přesně tak, jak to dělá handler rezervace.
        await using (ReservationsDbContext db = postgres.CreateDbContext())
        {
            RidePass pass = await db.RidePasses
                .Include(p => p.Transactions)
                .FirstAsync(p => p.Id == passId, token);

            pass.Consume(3, Guid.CreateVersion7(), Now);

            await db.SaveChangesAsync(token);
        }

        await using (ReservationsDbContext db = postgres.CreateDbContext())
        {
            RidePass pass = await db.RidePasses
                .Include(p => p.Transactions)
                .FirstAsync(p => p.Id == passId, token);

            pass.Balance.ShouldBe(7);
            pass.Transactions.Count.ShouldBe(2);
        }
    }

    [Fact]
    public async Task Vraceni_kreditu_prida_dalsi_pohyb_a_puvodni_nechá_beze_zmeny()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid passId;
        Guid reservationId = Guid.CreateVersion7();

        await using (ReservationsDbContext db = postgres.CreateDbContext())
        {
            RidePass pass = NewPass();
            pass.TopUp(5, Now);
            pass.Consume(2, reservationId, Now);
            db.RidePasses.Add(pass);
            await db.SaveChangesAsync(token);
            passId = pass.Id;
        }

        await using (ReservationsDbContext db = postgres.CreateDbContext())
        {
            RidePass pass = await db.RidePasses
                .Include(p => p.Transactions)
                .FirstAsync(p => p.Id == passId, token);

            pass.Refund(1, reservationId, Now, "Pozdní storno");

            await db.SaveChangesAsync(token);
        }

        await using (ReservationsDbContext db = postgres.CreateDbContext())
        {
            RidePass pass = await db.RidePasses
                .Include(p => p.Transactions)
                .FirstAsync(p => p.Id == passId, token);

            pass.Balance.ShouldBe(4);
            pass.Transactions.Count.ShouldBe(3);
            pass.Transactions.Count(t => t.Reason == CreditReason.RideConsumed).ShouldBe(1);
            pass.Transactions.Count(t => t.Reason == CreditReason.Refund).ShouldBe(1);
        }
    }
}
