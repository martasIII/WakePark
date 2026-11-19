using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Testcontainers.PostgreSql;

using WakePark.Modules.Identity.Infrastructure.Persistence;

namespace WakePark.Api.IntegrationTests;

/// <summary>
/// Testovací prostředí UAT: nginx se před každým požadavkem na web ptá
/// <c>/api/v1/identity/uat-access</c>, jestli přihlášený smí dál, a přihlašovací cookie
/// z backoffice musí dostat i web (doména <c>uat.wake-dzban.cz</c>).
/// </summary>
public sealed class UatAccessApiTests : IAsyncLifetime
{
    private const string AdminEmail = "admin@example.cz";
    private const string AdminPassword = "Testovaci-heslo-123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:WakeParkDb", _postgres.GetConnectionString());
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>()
            .SeedAsync(AdminEmail, AdminPassword, Guid.CreateVersion7(), TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Nepřihlášený_do_UAT_nesmí_admin_ano()
    {
        using HttpClient client = CreateClient(_factory);

        (await CheckAsync(client, cookie: null)).ShouldBe(HttpStatusCode.Unauthorized);

        string session = SessionCookie(await SignInAsync(client));
        (await CheckAsync(client, session[..session.IndexOf(';', StringComparison.Ordinal)])).ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>Totéž, co dělá nginx: GET s cookie z prohlížeče, zajímá ho jen stavový kód.</summary>
    private static async Task<HttpStatusCode> CheckAsync(HttpClient client, string? cookie)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/v1/identity/uat-access", UriKind.Relative));
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return (await client.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode;
    }

    [Fact]
    public async Task Cookie_platí_pro_doménu_z_nastavení_jinak_jen_pro_adresu_backoffice()
    {
        using HttpClient production = CreateClient(_factory);
        string productionCookie = SessionCookie(await SignInAsync(production));
        productionCookie.ShouldNotContain("domain=", Case.Insensitive);

        await using WebApplicationFactory<Program> uat = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:CookieDomain", "uat.wake-dzban.cz"));
        using HttpClient uatClient = CreateClient(uat);
        SessionCookie(await SignInAsync(uatClient)).ShouldContain("domain=uat.wake-dzban.cz", Case.Insensitive);
    }

    private static string SessionCookie(HttpResponseMessage login)
    {
        login.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wakepark.session=", StringComparison.Ordinal));
    }

    /// <summary>Přihlášení s antiforgery tokenem; bez automatických cookie se token i cookie posílají ručně.</summary>
    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client)
    {
        HttpResponseMessage health = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        string[] cookies = [.. health.Headers.GetValues("Set-Cookie").Select(c => c[..c.IndexOf(';', StringComparison.Ordinal)])];
        string xsrf = cookies.Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));

        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/v1/identity/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { Email = AdminEmail, Password = AdminPassword }),
        };
        request.Headers.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(xsrf["XSRF-TOKEN=".Length..]));
        request.Headers.Add("Cookie", string.Join("; ", cookies));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // Cookie se posílají ručně (jako je posílá nginx), proto klient bez vlastního úložiště cookie.
    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
}
