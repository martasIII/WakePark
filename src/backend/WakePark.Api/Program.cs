using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Scalar.AspNetCore;

using Serilog;

using WakePark.Api.Infrastructure;
using WakePark.Api.Infrastructure.Auth;
using WakePark.Api.OpenApi;
using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Infrastructure;
using WakePark.Modules.Catalog.Infrastructure;
using WakePark.Modules.Catalog.Infrastructure.Endpoints;
using WakePark.Modules.Identity.Infrastructure;
using WakePark.Modules.Identity.Infrastructure.Endpoints;
using WakePark.Modules.Operations.Infrastructure;
using WakePark.Modules.Operations.Infrastructure.Endpoints;
using WakePark.Modules.Reservations.Infrastructure;
using WakePark.Modules.Reservations.Infrastructure.Endpoints;
using WakePark.Modules.Tenancy.Infrastructure;
using WakePark.Modules.Tenancy.Infrastructure.Endpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

// --- Sdílená infrastruktura ---
builder.Services.AddHttpContextAccessor();
builder.Services.TryAddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.TryAddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddBuildingBlocks();

// --- Moduly ---
// Každý modul se registruje jednou metodou. Host o jeho vnitřku nic neví.
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddTenancyModule(builder.Configuration);
builder.Services.AddOperationsModule(builder.Configuration);
builder.Services.AddReservationsModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);

// --- Autentizace ---
// Session drží HttpOnly cookie. Odpovědí na neautentizovaný požadavek je stavový kód,
// ne přesměrování na přihlašovací stránku — tohle je API, ne web.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "wakepark.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = SessionSecurityStampValidator.ValidateAsync;
    });

// --- Autorizace ---
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<CurrentUserAccess>();

builder.Services.AddAntiforgery(options =>
{
    // Vlastní cookie ASP.NET Coru zůstává skrytá před JavaScriptem pod výchozím názvem.
    // Čitelnou cookie s request tokenem vypisuje AntiforgeryMiddleware; název hlavičky
    // odpovídá tomu, co Angular posílá sám od sebe.
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.HeaderName = "X-XSRF-TOKEN";
});

// --- HTTP ---
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();

// Enumy jdou po drátě jako text. Čísla by v JSON API znamenala, že klient musí znát pořadí
// hodnot a že vložení nové hodnoty doprostřed tiše změní význam dat.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ApiDocumentTransformer>());

WebApplication app = builder.Build();

// Krok nasazení: naimportuje katalog a skončí, server se nespouští.
if (args.Contains(CatalogImportExtensions.CommandName, StringComparer.Ordinal))
{
    bool imported = await app.ImportCatalogAsync().ConfigureAwait(false);
    return imported ? 0 : 1;
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseMiddleware<AntiforgeryMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // Jeden OpenAPI dokument, dvě čtečky. Swagger UI je zvyk, Scalar je čitelnější;
    // ať si každý vybere. Mimo vývoj se nepublikuje ani jedno.
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "WakePark API v1");
        options.DocumentTitle = "WakePark API";
        options.RoutePrefix = "swagger";
    });

    await app.SeedIdentityAsync().ConfigureAwait(false);
    await app.ImportCatalogAsync().ConfigureAwait(false);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithTags("Diagnostika")
    .AllowAnonymous();

app.MapIdentityEndpoints();
app.MapTenancyEndpoints();
app.MapOperationsEndpoints();
app.MapReservationsEndpoints();
app.MapCatalogEndpoints();

await app.RunAsync().ConfigureAwait(false);
return 0;

/// <summary>Vstupní bod API. Veřejný kvůli integračním testům přes <c>WebApplicationFactory</c>.</summary>
public partial class Program;
