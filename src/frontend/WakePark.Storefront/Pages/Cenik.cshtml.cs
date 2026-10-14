using Microsoft.AspNetCore.Mvc.RazorPages;

using WakePark.Storefront.Content;

namespace WakePark.Storefront.Pages;

public sealed partial class CenikModel(SiteContent content, ILogger<CenikModel> logger) : PageModel
{
    /// <summary>Null, když se ceník nepodařilo načíst — stránka pak ukáže telefon.</summary>
    public PriceList? PriceList { get; private set; }

    public void OnGet()
    {
        try
        {
            PriceList = content.PriceList;
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException)
        {
            LogLoadFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Ceník se nepodařilo načíst.")]
    private partial void LogLoadFailed(Exception ex);
}
