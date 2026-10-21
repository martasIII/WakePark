using System.Globalization;
using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Options;

using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Domain.Payments;

namespace WakePark.Modules.Billing.Infrastructure.Fio;

/// <summary>
/// Příchozí pohyby z Fio API (<c>/v1/rest/periods/{token}/{od}/{do}/transactions.json</c>).
/// </summary>
/// <remarks>
/// Token je součást adresy. HttpClient tohohle zdroje proto nesmí nic logovat (registrace
/// s <c>RemoveAllLoggers</c>) a výjimky se přebalují bez adresy.
/// </remarks>
internal sealed class FioBankStatementSource(HttpClient http, IOptions<FioOptions> options) : IBankStatementSource
{
    public async Task<IReadOnlyList<BankTransactionData>> GetIncomingAsync(
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken = default)
    {
        FioOptions settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Token))
        {
            throw new InvalidOperationException("Chybí token Fio API (Billing:Fio:Token).");
        }

        Uri url = new(
            settings.BaseUrl,
            string.Create(CultureInfo.InvariantCulture,
                $"/v1/rest/periods/{Uri.EscapeDataString(settings.Token)}/{firstDay:yyyy-MM-dd}/{lastDay:yyyy-MM-dd}/transactions.json"));

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // Zpráva výjimky může obsahovat adresu i s tokenem.
            throw new FioApiException($"Fio API není dostupné ({ex.HttpRequestError}).");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new FioApiException("Fio API: příliš častý dotaz (limit 1 dotaz za 30 s).");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new FioApiException($"Fio API odpovědělo {(int)response.StatusCode}.");
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Parse(json);
        }
    }

    /// <summary>
    /// Výpis Fio: <c>accountStatement.transactionList.transaction[]</c>, každé pole je
    /// <c>columnN: { value, name, id }</c> nebo <c>null</c>. Vrací jen příchozí pohyby.
    /// </summary>
    internal static IReadOnlyList<BankTransactionData> Parse(JsonDocument json)
    {
        JsonElement list = json.RootElement
            .GetProperty("accountStatement")
            .GetProperty("transactionList");

        if (!list.TryGetProperty("transaction", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<BankTransactionData> result = [];

        foreach (JsonElement item in items.EnumerateArray())
        {
            decimal amount = Column(item, Columns.Amount)?.GetDecimal() ?? 0m;
            if (amount <= 0m)
            {
                continue;
            }

            string externalId = Column(item, Columns.Id) is { } id
                ? id.ValueKind == JsonValueKind.Number ? id.GetInt64().ToString(CultureInfo.InvariantCulture) : id.GetString()!
                : throw new FioApiException("Pohyb z Fio nemá ID pohybu.");

            string account = Text(item, Columns.CounterpartyAccount) is { } number && Text(item, Columns.CounterpartyBankCode) is { } bank
                ? $"{number}/{bank}"
                : Text(item, Columns.CounterpartyAccount) ?? string.Empty;

            result.Add(new BankTransactionData(
                externalId,
                // Datum je ve tvaru „2026-10-22+0200“ — stačí den.
                DateOnly.ParseExact(Text(item, Columns.Date)![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                amount,
                Text(item, Columns.Currency) ?? "CZK",
                Text(item, Columns.VariableSymbol),
                account.Length == 0 ? null : account,
                Text(item, Columns.CounterpartyName),
                Text(item, Columns.Message) ?? Text(item, Columns.UserIdentification)));
        }

        return result;
    }

    private static JsonElement? Column(JsonElement item, int id) =>
        item.TryGetProperty($"column{id}", out JsonElement column)
        && column.ValueKind == JsonValueKind.Object
        && column.TryGetProperty("value", out JsonElement value)
        && value.ValueKind != JsonValueKind.Null
            ? value
            : null;

    private static string? Text(JsonElement item, int id) =>
        Column(item, id) is { } value
            ? (value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText())?.Trim() is { Length: > 0 } text ? text : null
            : null;

    /// <summary>Čísla sloupců podle dokumentace Fio API.</summary>
    private static class Columns
    {
        public const int Date = 0;
        public const int Amount = 1;
        public const int CounterpartyAccount = 2;
        public const int CounterpartyBankCode = 3;
        public const int VariableSymbol = 5;
        public const int UserIdentification = 7;
        public const int CounterpartyName = 10;
        public const int Currency = 14;
        public const int Message = 16;
        public const int Id = 22;
    }
}

/// <summary>Chyba Fio API. Zpráva nikdy neobsahuje token.</summary>
public sealed class FioApiException(string message) : Exception(message);
