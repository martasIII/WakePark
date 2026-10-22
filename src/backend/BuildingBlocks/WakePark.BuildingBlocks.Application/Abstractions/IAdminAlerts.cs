namespace WakePark.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Upozornění provozovateli (e-mail na info@), že něco potřebuje lidský zásah — platba,
/// která nesedí, e-mail, který nešel doručit, zpráva mezi moduly, která se nedoručila.
/// Řeší se ručně; bez upozornění by o tom nikdo nevěděl.
/// </summary>
/// <remarks>
/// Upozornění nesmí shodit to, co ho vyvolalo: když se nepovede ani upozornění, jen se zaloguje.
/// </remarks>
public interface IAdminAlerts
{
    Task SendAsync(string subject, string body, CancellationToken cancellationToken = default);
}
