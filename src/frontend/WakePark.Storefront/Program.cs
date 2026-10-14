using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.Extensions.WebEncoders;

using Serilog;

using WakePark.Storefront;
using WakePark.Storefront.Content;
using WakePark.Storefront.Infrastructure;
using WakePark.Storefront.Localization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

IConfigurationSection storefrontSection = builder.Configuration.GetSection(StorefrontOptions.SectionName);
builder.Services.Configure<StorefrontOptions>(storefrontSection);
StorefrontOptions storefront = storefrontSection.Get<StorefrontOptions>() ?? new StorefrontOptions();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services
    .AddRazorPages(options => options.Conventions.Add(
        new LanguageRouteConvention([.. storefront.SiteLanguages.Skip(1)])))
    .AddViewLocalization();

// Bez toho Razor kóduje každé písmeno s diakritikou jako &#x10D; — funkční, ale nečitelné.
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SiteContent>();
builder.Services.AddScoped<SiteView>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (string network in storefront.TrustedProxyNetworks)
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

WebApplication app = builder.Build();

app.UseForwardedHeaders();

// Logování hned na začátku, aby byla vidět i přesměrování starých adres — rewriter
// na ně odpoví sám a dál do pipeline je nepustí.
app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.Use(SecurityHeaders.Apply);
app.UseRewriter(LegacyRedirects.Create(storefront.ReservationUrl));
app.UseStatusCodePagesWithReExecute("/notfound");

app.UseRouting();

CultureInfo[] cultures = [.. storefront.SiteLanguages.Select(CultureInfo.GetCultureInfo)];
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(storefront.DefaultLanguage),
    SupportedCultures = cultures,
    SupportedUICultures = cultures,
    // Jazyk se bere jen z adresy. Ne z prohlížeče ani z cookie — stejná adresa musí
    // vždy vrátit stejný jazyk, jinak by vyhledávače indexovaly náhodnou verzi.
    RequestCultureProviders =
    [
        new RouteDataRequestCultureProvider
        {
            RouteDataStringKey = LanguageRouteConvention.RouteKey,
            UIRouteDataStringKey = LanguageRouteConvention.RouteKey,
        },
    ],
});

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapGet("/health", () => Results.Ok(new { status = "ok", role = "storefront" }));

await app.RunAsync().ConfigureAwait(false);

/// <summary>Vstupní bod; veřejný kvůli <c>WebApplicationFactory</c> v testech.</summary>
public partial class Program;
