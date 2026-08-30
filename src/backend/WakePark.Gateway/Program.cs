using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

// Jediný vstupní bod pro frontend: jeden origin, jedno místo pro CORS,
// autentizaci a rate limiting. Zatím směruje na jediný backend.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

WebApplication app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok", role = "gateway" }));
app.MapReverseProxy();

await app.RunAsync().ConfigureAwait(false);
