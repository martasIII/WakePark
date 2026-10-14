using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

using WakePark.Storefront.Content;
using WakePark.Storefront.Infrastructure;

namespace WakePark.Storefront.Pages;

public sealed partial class KontaktModel(
    SiteContent content,
    TimeProvider clock,
    IOptions<StorefrontOptions> options,
    ILogger<KontaktModel> logger) : PageModel
{
    /// <summary>Null, když se otevírací dobu nepodařilo načíst — stránka pak ukáže telefon.</summary>
    public OpeningHoursView? Hours { get; private set; }

    public void OnGet()
    {
        try
        {
            // „Dnes“ podle času v areálu, ne podle zóny serveru (kontejner běží v UTC).
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
            DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
            Hours = OpeningHoursView.Build(content.OpeningHours, today, SiteView.Culture);
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException)
        {
            LogLoadFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Otevírací dobu se nepodařilo načíst.")]
    private partial void LogLoadFailed(Exception ex);
}
