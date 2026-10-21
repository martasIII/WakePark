using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Options;

namespace WakePark.Storefront.Eshop;

// Vlastní kopie kontraktu API (Billing.Contracts) — web na moduly odkazovat nesmí.

public sealed record OrderRequest(string Language, OrderCustomer Customer, IReadOnlyList<OrderRequestLine> Lines, bool TermsAccepted);

public sealed record OrderCustomer(string Name, string Email, string? Phone, string? CompanyName, string? CompanyId, string? VatId);

public sealed record OrderRequestLine(Guid ProductId, int Quantity);

public sealed record PlacedOrder(long Number, string AccessToken);

public sealed record OrderView(
    long Number,
    string Status,
    string Language,
    string CustomerName,
    string CustomerEmail,
    string? CompanyName,
    IReadOnlyList<OrderViewLine> Lines,
    decimal TotalGross,
    decimal TotalVat,
    decimal TotalNet,
    string Currency,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset PaymentDueAtUtc,
    PaymentInstructions? Payment)
{
    public bool IsAwaitingPayment => Status == "AwaitingPayment";
}

public sealed record OrderViewLine(string ProductCode, string Name, int Quantity, decimal UnitPrice, decimal VatRate, decimal LineGross);

public sealed record PaymentInstructions(
    string AccountNumber,
    string Iban,
    string? Bic,
    string VariableSymbol,
    decimal Amount,
    string Currency,
    string Message,
    string Spayd);

/// <summary>Výsledek pokusu o objednávku: založená, nebo seznam chyb pro zákazníka.</summary>
public sealed record PlaceOrderResult(PlacedOrder? Order, IReadOnlyList<string> Errors)
{
    public static PlaceOrderResult Placed(PlacedOrder order) => new(order, []);

    public static PlaceOrderResult Rejected(IReadOnlyList<string> errors) => new(null, errors);
}

/// <summary>Objednávky přes API. Web objednávku jen předá — ceny a čísla určuje backend.</summary>
public sealed partial class OrderClient(HttpClient http, IOptions<StorefrontOptions> options, ILogger<OrderClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PlaceOrderResult> PlaceAsync(OrderRequest request, CancellationToken cancellationToken = default)
    {
        StorefrontOptions settings = options.Value;
        if (!settings.IsEshopConfigured)
        {
            return PlaceOrderResult.Rejected([UnavailableMessage]);
        }

        Uri url = new(settings.ApiBaseUrl!, $"/api/v1/billing/enterprises/{settings.EnterpriseId}/orders");

        try
        {
            using HttpResponseMessage response = await http
                .PostAsJsonAsync(url, request, Json, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                PlacedOrder? placed = await response.Content
                    .ReadFromJsonAsync<PlacedOrder>(Json, cancellationToken)
                    .ConfigureAwait(false);

                return placed is null ? PlaceOrderResult.Rejected([UnavailableMessage]) : PlaceOrderResult.Placed(placed);
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                return PlaceOrderResult.Rejected(await ReadProblemAsync(response, cancellationToken).ConfigureAwait(false));
            }

            LogUnexpectedStatus((int)response.StatusCode);
            return PlaceOrderResult.Rejected([UnavailableMessage]);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogFailed(ex);
            return PlaceOrderResult.Rejected([UnavailableMessage]);
        }
    }

    /// <returns>Null, když objednávka neexistuje nebo API neodpovídá.</returns>
    public async Task<OrderView?> GetAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        StorefrontOptions settings = options.Value;
        if (!settings.IsEshopConfigured)
        {
            return null;
        }

        Uri url = new(settings.ApiBaseUrl!, $"/api/v1/billing/orders/{Uri.EscapeDataString(accessToken)}");

        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OrderView>(Json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogFailed(ex);
            return null;
        }
    }

    /// <summary>Text pro zákazníka, když objednávka neprošla z technických důvodů.</summary>
    public const string UnavailableMessage = "Objednávku se teď nepodařilo odeslat. Zkuste to prosím za chvíli, nebo nám zavolejte.";

    private static async Task<IReadOnlyList<string>> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using JsonDocument problem = await JsonDocument
            .ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        List<string> messages = [];

        if (problem.RootElement.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty field in errors.EnumerateObject())
            {
                messages.AddRange(field.Value.EnumerateArray().Select(e => e.GetString()).OfType<string>());
            }
        }
        else if (problem.RootElement.TryGetProperty("detail", out JsonElement detail) && detail.GetString() is { } text)
        {
            messages.Add(text);
        }

        return messages.Count > 0 ? messages.Distinct(StringComparer.Ordinal).ToList() : [UnavailableMessage];
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "API objednávek odpovědělo neočekávaně: {Status}.")]
    private partial void LogUnexpectedStatus(int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Volání API objednávek selhalo.")]
    private partial void LogFailed(Exception ex);
}
