using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Identity.Infrastructure.Persistence;
using WakePark.Modules.Operations.Application.Setup;
using WakePark.Modules.Operations.Contracts;
using WakePark.Modules.Operations.Infrastructure.Persistence;
using WakePark.Modules.Tenancy.Infrastructure.Persistence;

namespace WakePark.Api.IntegrationTests;

/// <summary>
/// Provozní kalendář přes HTTP proti skutečnému PostgreSQL (ADR 0009): výchozí data
/// (provozovatel, areál s GPS, vleky s překážkami v jsonb), import CSV adminem, blokace
/// a veřejná otevírací doba bez interní poznámky.
/// </summary>
public sealed class OperationsApiTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly Guid ParkId = Guid.CreateVersion7();
    private const string AdminEmail = "admin@example.cz";
    private const string AdminPassword = "Testovaci-heslo-123";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:WakeParkDb", _container.GetConnectionString());
        });

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<TenancyDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<OperationsDbContext>().Database.MigrateAsync();

        // Totéž co příkaz import-setup s provoz.json.
        AddressSetup address = new("Pavlovská", "585/1", "Praha 6", "162 00", "CZ");
        await services.GetRequiredService<TenancySetupApplier>().ApplyAsync(
            new EnterpriseSetup(EnterpriseId, "Wake Park s.r.o.", "06633633", "CZ06633633", address, null, null),
            new ParkSetup(ParkId, "Wake Park Džbán", null, address with { Street = "Vodní nádrž Džbán", HouseNumber = null },
                "Europe/Prague", "CZK", "info@wake-dzban.cz", 50.095390m, 14.334290m),
            TestContext.Current.CancellationToken);
        await services.GetRequiredService<ICommandHandler<ApplyOperationsSetupCommand, OperationsSetupResult>>().HandleAsync(
            new ApplyOperationsSetupCommand(ParkId, new OperationsSetup(
                new ParkSettingsSetup(20, 3, 14, 30),
                [new CableSetup("vlek-1", "Vlek 1", null, 2, 180, [new ObstacleSetup("Kicker", "kicker", 40, null)])])),
            TestContext.Current.CancellationToken);

        // Totéž co příkaz create-admin; první admin se stane vlastníkem provozovatele.
        Guid adminId = await services.GetRequiredService<IdentitySeeder>()
            .EnsureAdminAsync(AdminEmail, AdminPassword, EnterpriseId, TestContext.Current.CancellationToken);
        (await services.GetRequiredService<TenancySetupApplier>().ClaimOwnershipAsync(EnterpriseId, adminId, TestContext.Current.CancellationToken))
            .ShouldBeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Vychozi_data_se_ulozi_i_s_GPS_a_prekazkami()
    {
        using HttpClient client = await SignedInClientAsync();

        IReadOnlyList<CableDetailDto> cables = (await client.GetFromJsonAsync<IReadOnlyList<CableDetailDto>>(
            new Uri($"/api/v1/operations/parks/{ParkId}/cables", UriKind.Relative), TestContext.Current.CancellationToken))!;

        cables.Single().Obstacles.ShouldBe([new ObstacleDto("Kicker", "kicker", 40, null)]);

        using IServiceScope scope = _factory.Services.CreateScope();
        TenancyDbContext tenancy = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        (await tenancy.Parks.SingleAsync(TestContext.Current.CancellationToken)).Location!.Latitude.ShouldBe(50.095390m);
    }

    [Fact]
    public async Task Admin_nahraje_CSV_zablokuje_vlek_a_web_vidi_otevraci_dobu_bez_poznamky()
    {
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague")).DateTime);
        using HttpClient client = await SignedInClientAsync();

        OperatingDaysImportDto preview = await PostAsync<OperatingDaysImportDto>(client, $"/api/v1/operations/parks/{ParkId}/days/import/preview",
            new OperatingDaysImportRequest($"datum;od;do\n{today:yyyy-MM-dd};10:00;19:00\n{today.AddDays(1):yyyy-MM-dd};10:00;19:00\n"));
        preview.Added.ShouldBe(2);

        OperatingDaysImportDto imported = await PostAsync<OperatingDaysImportDto>(client, $"/api/v1/operations/parks/{ParkId}/days/import",
            new OperatingDaysImportRequest($"{today:yyyy-MM-dd};10:00;19:00\n{today.AddDays(1):yyyy-MM-dd};10:00;19:00\n"));
        imported.Added.ShouldBe(2);

        BlockingDto blocking = await PostAsync<BlockingDto>(client, $"/api/v1/operations/parks/{ParkId}/blockings",
            new CreateBlockingRequest(null, today.AddDays(1).ToDateTime(new TimeOnly(9, 0)), today.AddDays(1).ToDateTime(new TimeOnly(12, 0)), "Weather", "Předpověď vichřice"));
        blocking.Note.ShouldBe("Předpověď vichřice");

        // Veřejně, bez přihlášení.
        using HttpClient anonymous = _factory.CreateClient();
        string json = await anonymous.GetStringAsync(new Uri($"/api/v1/operations/parks/{ParkId}/opening-hours", UriKind.Relative), TestContext.Current.CancellationToken);
        PublicOpeningHoursDto hours = System.Text.Json.JsonSerializer.Deserialize<PublicOpeningHoursDto>(json, Json)!;

        hours.Days.Count.ShouldBe(14);
        hours.Days[0].OpensAt.ShouldBe(new TimeOnly(10, 0));
        hours.Days[1].Blockings.ShouldBe([new PublicBlockingDto(null, new TimeOnly(10, 0), new TimeOnly(12, 0), "Weather")]);
        json.ShouldNotContain("vichřice", Case.Insensitive, "interní poznámka nesmí na web");
    }

    [Fact]
    public async Task Spravovat_kalendar_bez_prihlaseni_ani_cizi_areal_nejde()
    {
        using HttpClient anonymous = _factory.CreateClient();
        (await anonymous.GetAsync(new Uri($"/api/v1/operations/parks/{ParkId}/calendar?from=2027-05-01&to=2027-05-31", UriKind.Relative), TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using HttpClient client = await SignedInClientAsync();
        (await client.GetAsync(new Uri($"/api/v1/operations/parks/{Guid.CreateVersion7()}/calendar?from=2027-05-01&to=2027-05-31", UriKind.Relative), TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Přihlašovací cookie je Secure — přes http by ji klient zpátky neposlal.
    private async Task<HttpClient> SignedInClientAsync()
    {
        HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        HttpResponseMessage login = await RawPostAsync(client, "/api/v1/identity/login", new { Email = AdminEmail, Password = AdminPassword });
        login.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return client;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body)
    {
        HttpResponseMessage response = await RawPostAsync(client, path, body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    /// <summary>POST jako z prohlížeče: antiforgery token z cookie zpátky v hlavičce.</summary>
    private static async Task<HttpResponseMessage> RawPostAsync(HttpClient client, string path, object body)
    {
        HttpResponseMessage get = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        string cookie = get.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        string token = Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';', StringComparison.Ordinal)]);

        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
