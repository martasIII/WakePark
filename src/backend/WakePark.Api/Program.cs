using Microsoft.Extensions.DependencyInjection.Extensions;

using Scalar.AspNetCore;

using Serilog;

using WakePark.Api.Infrastructure;
using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Infrastructure;
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
builder.Services.AddBuildingBlocks();

// --- Moduly ---
// Každý modul se registruje jednou metodou. Host o jeho vnitřku nic neví.
builder.Services.AddTenancyModule(builder.Configuration);
builder.Services.AddOperationsModule(builder.Configuration);
builder.Services.AddReservationsModule(builder.Configuration);

// --- HTTP ---
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithTags("Diagnostika");

app.MapTenancyEndpoints();
app.MapOperationsEndpoints();
app.MapReservationsEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Vstupní bod API. Veřejný kvůli integračním testům přes <c>WebApplicationFactory</c>.</summary>
public partial class Program;
