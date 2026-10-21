using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Invoices;

namespace WakePark.Modules.Billing.Application.Invoices;

internal sealed class InvoiceDirectory(IInvoiceRepository invoices) : IInvoiceDirectory
{
    public async Task<InvoiceDto?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await invoices.GetByOrderAsync(orderId, cancellationToken).ConfigureAwait(false) is { } invoice
            ? ToDto(invoice)
            : null;

    internal static InvoiceDto ToDto(Invoice invoice) => new(
        invoice.Number,
        invoice.IssuedOn,
        invoice.TaxableSupplyOn,
        invoice.VariableSymbol,
        invoice.Currency,
        Party(invoice.Seller),
        Party(invoice.Buyer),
        [.. invoice.Lines.Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.Net, l.Vat, l.Gross))],
        [.. invoice.Lines
            .GroupBy(l => l.VatRate)
            .OrderByDescending(g => g.Key)
            .Select(g => new InvoiceVatDto(g.Key, g.Sum(l => l.Net), g.Sum(l => l.Vat), g.Sum(l => l.Gross)))],
        invoice.TotalNet,
        invoice.TotalVat,
        invoice.TotalGross);

    private static InvoicePartyDto Party(InvoiceParty party) =>
        new(party.Name, party.CompanyId, party.VatId, party.Address, party.Registration, party.ContactName, party.Email);
}
