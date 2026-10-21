using QRCoder;

namespace WakePark.Storefront.Eshop;

/// <summary>
/// QR kód pro platbu jako SVG vložené přímo do stránky — bez JavaScriptu, bez dalšího
/// požadavku a bez povolování dalších zdrojů v CSP.
/// </summary>
public static class PaymentQrCode
{
    public static string Svg(string spayd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spayd);

        // Úroveň opravy M: snese drobné poškození (odlesk displeje), kód zůstane malý.
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(spayd, QRCodeGenerator.ECCLevel.M);
        using SvgQRCode qr = new(data);

        return qr.GetGraphic(new System.Drawing.Size(240, 240), "#000000", "#ffffff", drawQuietZones: true);
    }

    /// <summary>
    /// Totéž jako PNG ke stažení. Kdo objednává z mobilu, nemá čím QR na displeji naskenovat —
    /// uloží si obrázek a bankovní aplikace ho načte z galerie.
    /// </summary>
    public static byte[] Png(string spayd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spayd);

        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(spayd, QRCodeGenerator.ECCLevel.M);
        using PngByteQRCode qr = new(data);

        return qr.GetGraphic(12);
    }
}
