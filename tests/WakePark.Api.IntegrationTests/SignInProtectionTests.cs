using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

using WakePark.Modules.Identity.Infrastructure.Persistence;

namespace WakePark.Api.IntegrationTests;

/// <summary>
/// Ochrana přihlášení na veřejném backoffice: zamčení účtu po špatných heslech, cesta zpátky
/// přes <c>create-admin</c> a limit pokusů z jedné adresy.
/// </summary>
public sealed class SignInProtectionTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private const string AdminEmail = "admin@example.cz";
    private const string AdminPassword = "Testovaci-heslo-123";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

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
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>()
            .EnsureAdminAsync(AdminEmail, AdminPassword, EnterpriseId, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Po_peti_spatnych_heslech_se_ucet_zamkne_a_create_admin_ho_odemkne()
    {
        using HttpClient client = CreateClient(_factory);

        for (int i = 0; i < 5; i++)
        {
            (await SignInAsync(client, AdminPassword + "x")).ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await SignInAsync(client, AdminPassword)).ShouldBe(HttpStatusCode.Unauthorized, "zamčený účet nepustí ani se správným heslem");

        // Totéž co příkaz create-admin pro existující účet.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentitySeeder>()
                .EnsureAdminAsync(AdminEmail, "Nove-heslo-456789", EnterpriseId, TestContext.Current.CancellationToken);
        }

        (await SignInAsync(client, AdminPassword)).ShouldBe(HttpStatusCode.Unauthorized, "staré heslo už neplatí");
        (await SignInAsync(client, "Nove-heslo-456789")).ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Z_jedne_adresy_jde_jen_omezeny_pocet_pokusu()
    {
        await using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:SignInRateLimit:PermitLimit", "3"));
        using HttpClient client = CreateClient(factory);

        for (int i = 0; i < 3; i++)
        {
            (await SignInAsync(client, "spatne-heslo-" + i, "nikdo" + i + "@example.cz")).ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await SignInAsync(client, AdminPassword)).ShouldBe(HttpStatusCode.TooManyRequests, "limit platí i pro správné heslo");
    }

    // Přihlašovací cookie je Secure — přes http by ji klient zpátky neposlal.
    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>Přihlášení jako z prohlížeče: antiforgery token z cookie zpátky v hlavičce.</summary>
    private static async Task<HttpStatusCode> SignInAsync(HttpClient client, string password, string email = AdminEmail)
    {
        HttpResponseMessage get = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        string cookie = get.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        string token = Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';', StringComparison.Ordinal)]);

        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/v1/identity/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { Email = email, Password = password }),
        };
        request.Headers.Add("X-XSRF-TOKEN", token);
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }
}
