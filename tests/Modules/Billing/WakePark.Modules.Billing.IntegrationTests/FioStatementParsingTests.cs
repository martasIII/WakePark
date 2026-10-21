using System.Text.Json;

using Shouldly;

using WakePark.Modules.Billing.Domain.Payments;
using WakePark.Modules.Billing.Infrastructure.Fio;

namespace WakePark.Modules.Billing.IntegrationTests;

/// <summary>Výpis ve formátu Fio API (bez sítě a bez databáze).</summary>
public sealed class FioStatementParsingTests
{
    private const string Statement = """
        {
          "accountStatement": {
            "info": { "accountId": "2601338268", "bankId": "2010", "currency": "CZK" },
            "transactionList": {
              "transaction": [
                {
                  "column22": { "value": 26512345678, "name": "ID pohybu", "id": 22 },
                  "column0": { "value": "2026-10-22+0200", "name": "Datum", "id": 0 },
                  "column1": { "value": 2580.00, "name": "Objem", "id": 1 },
                  "column14": { "value": "CZK", "name": "Měna", "id": 14 },
                  "column2": { "value": "1234567890", "name": "Protiúčet", "id": 2 },
                  "column3": { "value": "0800", "name": "Kód banky", "id": 3 },
                  "column10": { "value": "Nováková Jana", "name": "Název protiúčtu", "id": 10 },
                  "column5": { "value": "261000", "name": "VS", "id": 5 },
                  "column7": { "value": "Nováková Jana", "name": "Uživatelská identifikace", "id": 7 },
                  "column16": { "value": "Wake Park objednavka 261000", "name": "Zpráva pro příjemce", "id": 16 },
                  "column8": { "value": "Bezhotovostní příjem", "name": "Typ", "id": 8 }
                },
                {
                  "column22": { "value": 26512345679, "name": "ID pohybu", "id": 22 },
                  "column0": { "value": "2026-10-22+0200", "name": "Datum", "id": 0 },
                  "column1": { "value": -1500.00, "name": "Objem", "id": 1 },
                  "column14": { "value": "CZK", "name": "Měna", "id": 14 },
                  "column5": null,
                  "column16": null
                },
                {
                  "column22": { "value": 26512345680, "name": "ID pohybu", "id": 22 },
                  "column0": { "value": "2026-10-23+0200", "name": "Datum", "id": 0 },
                  "column1": { "value": 690.00, "name": "Objem", "id": 1 },
                  "column14": { "value": "CZK", "name": "Měna", "id": 14 },
                  "column5": null,
                  "column16": null,
                  "column7": { "value": "Platba za kurz", "name": "Uživatelská identifikace", "id": 7 }
                }
              ]
            }
          }
        }
        """;

    [Fact]
    public void Prichozi_pohyby_se_prectou_a_odchozi_se_vynechaji()
    {
        using JsonDocument json = JsonDocument.Parse(Statement);

        IReadOnlyList<BankTransactionData> transactions = FioBankStatementSource.Parse(json);

        transactions.Count.ShouldBe(2);

        BankTransactionData payment = transactions[0];
        payment.ExternalId.ShouldBe("26512345678");
        payment.BookedOn.ShouldBe(new DateOnly(2026, 10, 22));
        payment.Amount.ShouldBe(2580m);
        payment.Currency.ShouldBe("CZK");
        payment.VariableSymbol.ShouldBe("261000");
        payment.CounterpartyAccount.ShouldBe("1234567890/0800");
        payment.CounterpartyName.ShouldBe("Nováková Jana");
        payment.Message.ShouldBe("Wake Park objednavka 261000");

        transactions[1].VariableSymbol.ShouldBeNull();
        transactions[1].Message.ShouldBe("Platba za kurz", "bez zprávy pro příjemce se vezme identifikace");
    }

    [Fact]
    public void Prazdny_vypis_neni_chyba()
    {
        using JsonDocument json = JsonDocument.Parse("""{ "accountStatement": { "transactionList": { "transaction": [] } } }""");

        FioBankStatementSource.Parse(json).ShouldBeEmpty();
    }
}
