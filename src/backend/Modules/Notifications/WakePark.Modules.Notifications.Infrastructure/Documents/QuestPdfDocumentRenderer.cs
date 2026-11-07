using System.Globalization;

using QRCoder;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;
using WakePark.Modules.Vouchers.Contracts;

using RectangleF = System.Drawing.RectangleF;

namespace WakePark.Modules.Notifications.Infrastructure.Documents;

/// <summary>
/// PDF poukazů a daňových dokladů (QuestPDF). Vzhled je provizorní — finální podobu navrhne
/// grafik; barvy poukazů podle produktu jsou v konfiguraci, ne v kódu.
/// </summary>
internal sealed class QuestPdfDocumentRenderer : IDocumentRenderer
{
    private const string Brand = "#e50051";
    private const string Muted = "#6b6b72";

    private static readonly CultureInfo Czech = CultureInfo.GetCultureInfo("cs");

    private static readonly string[] LineHeaders = ["Položka", "Ks", "Cena/ks s DPH", "Sazba", "Základ", "DPH", "Celkem"];

    private static readonly string[] VatHeaders = ["Sazba", "Základ", "DPH", "Celkem"];

    private readonly NotificationsOptions _options;

    static QuestPdfDocumentRenderer()
    {
        // Community licence: zdarma pro firmy s obratem pod 1 mil. USD (ADR 0006).
        QuestPDF.Settings.License = LicenseType.Community;

        // Šablony poukazů mají kód a platnost v Arialu. Na serveru Arial není, Liberation Sans
        // má stejné rozměry písmen (licence SIL OFL, soubor vedle písma).
        System.Reflection.Assembly assembly = typeof(QuestPdfDocumentRenderer).Assembly;
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.Ordinal)))
        {
            using Stream font = assembly.GetManifestResourceStream(name)!;
            QuestPDF.Drawing.FontManager.RegisterFontFromStream(font);
        }
    }

    public QuestPdfDocumentRenderer(NotificationsOptions options)
    {
        _options = options;
    }

    public byte[] VoucherPdf(VoucherDto voucher)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        return TemplatePath(voucher.ProductCode) is { } template ? TemplateVoucherPdf(voucher, template) : GenericVoucherPdf(voucher);
    }

    public EmailAttachment? VoucherPrintGuide(IReadOnlyList<VoucherDto> vouchers)
    {
        ArgumentNullException.ThrowIfNull(vouchers);
        if (_options.VoucherPrintGuide is not { } guide || !vouchers.Any(v => TemplatePath(v.ProductCode) is not null))
        {
            return null;
        }

        string path = Path.Combine(AppContext.BaseDirectory, _options.VoucherTemplateDirectory, guide);
        return new EmailAttachment(guide, MediaType(guide), File.ReadAllBytes(path));
    }

    /// <summary>Šablona k produktu, nebo <c>null</c> — pak se poukaz vykreslí obecně.</summary>
    private string? TemplatePath(string productCode) =>
        _options.VoucherTemplates.TryGetValue(productCode, out string? file)
            ? Path.Combine(AppContext.BaseDirectory, _options.VoucherTemplateDirectory, file)
            : null;

    /// <summary>
    /// Poukaz ze šablony od grafika (ADR 0006): přes první stranu A4 se položí průhledná vrstva
    /// s kódem, koncem platnosti a věnováním, grafika zůstane vektorová. Místa jsou na všech
    /// šablonách stejná. Prostřední pruh se při skládání otáčí — kód a platnost jsou proto
    /// vzhůru nohama jako ostatní texty v něm.
    /// </summary>
    private static byte[] TemplateVoucherPdf(VoucherDto voucher, string templatePath)
    {
        string layer = Path.GetTempFileName();
        string output = Path.GetTempFileName();
        try
        {
            Document.Create(document => document.Page(page =>
            {
                page.Size(new PageSize(TemplateLayout.PageWidth, TemplateLayout.PageHeight));
                page.Margin(0);
                page.PageColor(Colors.Transparent);
                page.DefaultTextStyle(t => t.FontFamily(TemplateLayout.Font).FontColor(Colors.White));

                page.Content().Layers(layers =>
                {
                    layers.PrimaryLayer();

                    // Texty vzhůru nohama: převrácení vodorovně i svisle = otočení o 180° uvnitř
                    // místa prvku. Zarovnání vlevo/nahoře platí při čtení.
                    Place(layers.Layer(), TemplateLayout.CodeBox).FlipHorizontal().FlipVertical()
                        .AlignCenter().AlignMiddle().Text(voucher.Code).FontSize(18);

                    Place(layers.Layer(), TemplateLayout.ValidToLine).FlipHorizontal().FlipVertical()
                        .AlignLeft().AlignTop().Text(voucher.ValidTo.ToString("d. M. yyyy", Czech)).FontSize(10);

                    if (voucher.Dedication is { Length: > 0 } dedication)
                    {
                        Place(layers.Layer(), TemplateLayout.Dedication)
                            .AlignCenter().Text($"„{dedication}“").FontSize(10.5f).Italic().FontColor(Brand).AlignCenter();
                    }
                });
            })).GeneratePdf(layer);

            DocumentOperation.LoadFile(templatePath)
                .OverlayFile(new DocumentOperation.LayerConfiguration { FilePath = layer, TargetPages = "1" })
                .Save(output);
            return File.ReadAllBytes(output);
        }
        finally
        {
            File.Delete(layer);
            File.Delete(output);
        }
    }

    private static IContainer Place(IContainer layer, RectangleF area) =>
        layer.OffsetX(area.Left).OffsetY(area.Top).Width(area.Width).Height(area.Height);

    private static string MediaType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };

    /// <summary>Obecný poukaz — pro produkty, ke kterým grafik šablonu nedodal (zatím permanentky).</summary>
    private byte[] GenericVoucherPdf(VoucherDto voucher)
    {
        bool en = voucher.Language == "en";
        string color = _options.VoucherColors.TryGetValue(voucher.ProductCode, out string? c) ? c : Brand;
        CultureInfo culture = CultureInfo.GetCultureInfo(en ? "en" : "cs");
        string Date(DateOnly date) => date.ToString(en ? "d MMM yyyy" : "d. M. yyyy", culture);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A5.Landscape());
            page.Margin(0);
            page.DefaultTextStyle(t => t.FontSize(11).FontColor(Colors.Black));

            page.Content().Row(row =>
            {
                // Barevný pruh podle produktu — ať je na první pohled vidět 1/2/3.
                row.ConstantItem(70).Background(color).AlignMiddle().AlignCenter()
                    .RotateLayoutCounterclockwise().Text("WAKE PARK DŽBÁN").FontColor(Colors.White).Bold().FontSize(16).LetterSpacing(0.15f);

                row.RelativeItem().Padding(28).Column(column =>
                {
                    column.Item().Text(en ? "GIFT VOUCHER" : "DÁRKOVÝ VOUCHER").FontColor(color).Bold().FontSize(13).LetterSpacing(0.1f);
                    column.Item().PaddingTop(6).Text(voucher.DisplayName).Bold().FontSize(26);

                    if (voucher.Dedication is { Length: > 0 } dedication)
                    {
                        column.Item().PaddingTop(8).Text($"„{dedication}“").Italic().FontSize(13);
                    }

                    column.Item().PaddingTop(18).Row(inner =>
                    {
                        inner.RelativeItem().Column(details =>
                        {
                            details.Item().Text(en ? "Voucher code" : "Kód voucheru").FontColor(Muted).FontSize(10);
                            details.Item().Text(voucher.Code).Bold().FontSize(22).LetterSpacing(0.08f);

                            details.Item().PaddingTop(14).Text(en ? "Valid" : "Platnost").FontColor(Muted).FontSize(10);
                            details.Item().Text($"{Date(voucher.ValidFrom)} – {Date(voucher.ValidTo)}").Bold().FontSize(13);

                            details.Item().PaddingTop(14).Text(en
                                ? $"Book your ride in advance: {_options.Phone}"
                                : $"Jízdu si předem zarezervujte: {_options.Phone}").FontSize(10);
                        });

                        inner.ConstantItem(120).AlignBottom().Image(QrPng(voucher.Code));
                    });

                    column.Item().ExtendVertical().AlignBottom().Text(text =>
                    {
                        text.DefaultTextStyle(t => t.FontSize(8).FontColor(Muted));
                        text.Span(en
                            ? $"Wake Park Džbán, Prague 6 · order {voucher.OrderNumber} · one-time use, transferable."
                            : $"Wake Park Džbán, Praha 6-Vokovice · objednávka {voucher.OrderNumber} · uplatní se jednorázově, je přenosný.");
                    });
                });
            });
        })).GeneratePdf();
    }

    public byte[] InvoicePdf(InvoiceDto invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        string Money(decimal amount) => amount.ToString("#,0.00", Czech) + " Kč";
        string Date(DateOnly date) => date.ToString("d. M. yyyy", Czech);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Faktura — daňový doklad").Bold().FontSize(18);
                    column.Item().Text($"č. {invoice.Number}").FontSize(13).FontColor(Brand).Bold();
                });
                row.ConstantItem(180).AlignRight().Text("WAKE PARK DŽBÁN").Bold().FontColor(Brand).FontSize(14);
            });

            page.Content().PaddingTop(24).Column(column =>
            {
                column.Spacing(18);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => Party(c, "Prodávající", invoice.Seller));
                    row.ConstantItem(24);
                    row.RelativeItem().Element(c => Party(c, "Kupující", invoice.Buyer));
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });
                    Meta(table, "Datum vystavení", Date(invoice.IssuedOn));
                    Meta(table, "Datum uskutečnění plnění", Date(invoice.TaxableSupplyOn));
                    Meta(table, "Variabilní symbol", invoice.VariableSymbol);
                    Meta(table, "Forma úhrady", "převodem — uhrazeno");
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        for (int i = 0; i < LineHeaders.Length; i++)
                        {
                            // Popis a počet kusů doleva, částky doprava — stejně jako hodnoty pod nimi.
                            IContainer cell = header.Cell().BorderBottom(1).PaddingBottom(4);
                            (i < 2 ? cell : cell.AlignRight()).Text(LineHeaders[i]).Bold().FontSize(9);
                        }
                    });

                    foreach (InvoiceLineDto line in invoice.Lines)
                    {
                        table.Cell().PaddingVertical(4).Text(line.Description);
                        table.Cell().PaddingVertical(4).Text(line.Quantity.ToString(Czech));
                        table.Cell().PaddingVertical(4).AlignRight().Text(Money(line.UnitPrice));
                        table.Cell().PaddingVertical(4).AlignRight().Text($"{line.VatRate:0.##} %");
                        table.Cell().PaddingVertical(4).AlignRight().Text(Money(line.Net));
                        table.Cell().PaddingVertical(4).AlignRight().Text(Money(line.Vat));
                        table.Cell().PaddingVertical(4).AlignRight().Text(Money(line.Gross));
                    }
                });

                column.Item().AlignRight().Width(280).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                        c.RelativeColumn(3);
                        c.RelativeColumn(3);
                    });
                    table.Header(header =>
                    {
                        foreach (string title in VatHeaders)
                        {
                            header.Cell().BorderBottom(1).PaddingBottom(4).AlignRight().Text(title).Bold().FontSize(9);
                        }
                    });
                    foreach (InvoiceVatDto vat in invoice.VatRecap)
                    {
                        table.Cell().AlignRight().Text($"{vat.VatRate:0.##} %");
                        table.Cell().AlignRight().Text(Money(vat.Net));
                        table.Cell().AlignRight().Text(Money(vat.Vat));
                        table.Cell().AlignRight().Text(Money(vat.Gross));
                    }
                });

                column.Item().AlignRight().Text(text =>
                {
                    text.Span("Celkem k úhradě: ").FontSize(12);
                    text.Span(Money(invoice.TotalGross)).Bold().FontSize(14);
                });
                column.Item().AlignRight().Text("Uhrazeno — neplaťte.").FontColor("#1a8f52").Bold();

                column.Item().PaddingTop(12).Text(
                    // Přesné znění poznámky potvrdí účetní — žádný odkaz na paragraf bez jejího ověření.
                    "Předmětem plnění je jednoúčelový poukaz. Plnění se považuje za uskutečněné dnem přijetí úplaty.")
                    .FontSize(8).FontColor(Muted);
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                t.Span($"{invoice.Seller.Name} · IČO {invoice.Seller.CompanyId} · {invoice.Seller.Registration}");
            });
        })).GeneratePdf();
    }

    private static void Party(IContainer container, string title, InvoicePartyDto party) =>
        container.Column(column =>
        {
            column.Item().Text(title).FontColor(Muted).FontSize(9);
            column.Item().Text(party.Name).Bold().FontSize(12);
            if (party.ContactName is not null)
            {
                column.Item().Text(party.ContactName);
            }

            if (party.Address is not null)
            {
                column.Item().Text(party.Address);
            }

            if (party.CompanyId is not null)
            {
                column.Item().Text($"IČO: {party.CompanyId}");
            }

            if (party.VatId is not null)
            {
                column.Item().Text($"DIČ: {party.VatId}");
            }

            if (party.Email is not null)
            {
                column.Item().Text(party.Email);
            }
        });

    private static void Meta(TableDescriptor table, string label, string value)
    {
        table.Cell().Column(c =>
        {
            c.Item().Text(label).FontColor(Muted).FontSize(8);
            c.Item().Text(value).Bold();
        });
    }

    private static byte[] QrPng(string content)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using PngByteQRCode qr = new(data);
        return qr.GetGraphic(10);
    }

    /// <summary>
    /// Místa na první straně šablony v bodech (A4, počátek vlevo nahoře), změřená v PDF od
    /// grafika (VOUCHER20-2027). Platí pro všechny šablony — liší se jen barvou a textem.
    /// </summary>
    private static class TemplateLayout
    {
        public const float PageWidth = 595.276f;
        public const float PageHeight = 841.89f;
        public const string Font = "Liberation Sans";

        /// <summary>Černý rámeček pro kód.</summary>
        public static readonly RectangleF CodeBox = RectangleF.FromLTRB(340.91f, 431.15f, 538.58f, 458.75f);

        /// <summary>
        /// Řádek pod „Voucher je možné využít do“ (vzhůru nohama). Pravý okraj je začátek řádku
        /// při čtení, zarovnaný s textem nad ním.
        /// </summary>
        public static readonly RectangleF ValidToLine = RectangleF.FromLTRB(338.58f, 385.9f, 538.58f, 398.3f);

        /// <summary>
        /// Volné místo pod úvodním odstavcem „Gratulujeme!“, mezi černou vlnou a fotkou —
        /// 120 znaků se vejde na 4–5 řádků.
        /// </summary>
        public static readonly RectangleF Dedication = RectangleF.FromLTRB(92f, 190f, 255f, 276f);
    }
}
