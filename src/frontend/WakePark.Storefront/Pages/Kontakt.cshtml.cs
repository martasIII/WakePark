using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

using WakePark.Storefront.Content;
using WakePark.Storefront.Infrastructure;
using WakePark.Storefront.Localization;

namespace WakePark.Storefront.Pages;

public sealed partial class KontaktModel(
    SiteContent content,
    OpeningHoursClient openingHoursClient,
    IStringLocalizer<SharedResource> text,
    TimeProvider clock,
    IOptions<StorefrontOptions> options,
    ILogger<KontaktModel> logger) : PageModel
{
    /// <summary>Null, když se otevírací dobu nepodařilo načíst — stránka pak ukáže telefon.</summary>
    public OpeningHoursView? Hours { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // „Dnes“ podle času v areálu, ne podle zóny serveru (kontejner běží v UTC).
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);

        try
        {
            if (options.Value.IsOpeningHoursApiConfigured)
            {
                // Provozní dny z backoffice (ADR 0009); obecná poznámka zůstává v souboru webu.
                WebOpeningHours? hours = await openingHoursClient.GetAsync(cancellationToken).ConfigureAwait(false);
                Hours = hours is null
                    ? null
                    : OpeningHoursView.BuildFromApi(
                        hours,
                        today,
                        content.OpeningHours.Note?.Resolve(SiteView.Language),
                        SiteView.Culture,
                        key => text[key].Value);
            }
            else
            {
                Hours = OpeningHoursView.Build(content.OpeningHours, today, SiteView.Culture);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException)
        {
            LogLoadFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Otevírací dobu se nepodařilo načíst.")]
    private partial void LogLoadFailed(Exception ex);
}
