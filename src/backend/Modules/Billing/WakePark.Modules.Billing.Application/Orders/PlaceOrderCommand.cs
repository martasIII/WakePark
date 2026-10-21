using System.Security.Cryptography;

using FluentValidation;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Billing.Application.Orders;

/// <summary>Založí objednávku z webu. Ceny a názvy bere z aktuální nabídky, ne z požadavku.</summary>
public sealed record PlaceOrderCommand(Guid EnterpriseId, PlaceOrderRequest Request) : ICommand<PlacedOrderDto>;

internal sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(c => c.Request.TermsAccepted).Equal(true)
            .WithMessage("Pro objednání je potřeba souhlasit s obchodními podmínkami.");

        RuleFor(c => c.Request.Lines).NotEmpty()
            .WithMessage("Vyberte aspoň jeden produkt.");

        RuleFor(c => c.Request.Lines.Count).LessThanOrEqualTo(Order.MaxLines)
            .WithMessage("Objednávka má příliš mnoho položek.");

        RuleForEach(c => c.Request.Lines).ChildRules(line =>
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, Order.MaxQuantityPerLine)
                .WithMessage($"Počet kusů jedné položky musí být 1 až {Order.MaxQuantityPerLine}."));

        RuleFor(c => c.Request.Language).Matches("^[a-z]{2}$")
            .WithMessage("Neznámý jazyk.");

        RuleFor(c => c.Request.Customer).NotNull();
    }
}

internal sealed class PlaceOrderCommandHandler(
    ICatalogReader catalog,
    IOrderNumberAllocator numbers,
    IOrderRepository orders,
    IBillingUnitOfWork unitOfWork,
    IClock clock,
    BillingOptions options)
    : ICommandHandler<PlaceOrderCommand, PlacedOrderDto>
{
    private const string DocumentLanguage = "cs";

    private readonly PlaceOrderCommandValidator _validator = new();

    public async Task<PlacedOrderDto> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        PlaceOrderRequest request = command.Request;

        // Kontakt se ověří dřív, než se přidělí číslo — překlep v e-mailu nemá spotřebovat číslo z řady.
        PlaceOrderCustomer input = request.Customer;
        CustomerContact customer = CustomerContact.Create(
            input.Name, input.Email, input.Phone, input.CompanyName, input.CompanyId, input.VatId);

        CatalogDto offer = await catalog
            .GetPublishedCatalogAsync(command.EnterpriseId, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, CatalogProductDto> products = offer.Products.ToDictionary(p => p.Id);
        List<OrderLineDraft> lines = [];

        foreach (PlaceOrderLine line in request.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out CatalogProductDto? product))
            {
                throw new ValidationException(
                    "Některý z vybraných produktů už není v nabídce. Obnovte stránku a vyberte znovu.");
            }

            lines.Add(new OrderLineDraft(
                product.Id,
                product.Code,
                Text(product.Name, DocumentLanguage),
                Text(product.Name, request.Language),
                line.Quantity,
                product.UnitPrice,
                product.VatRate));
        }

        DateTimeOffset now = clock.UtcNow;
        int year = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId)).Year;

        long sequence = await numbers
            .NextSequenceAsync(command.EnterpriseId, year, cancellationToken)
            .ConfigureAwait(false);

        Order order = Order.Place(
            command.EnterpriseId,
            OrderNumber.Compose(year, sequence),
            NewAccessToken(),
            customer,
            request.Language,
            products[lines[0].ProductId].Currency,
            lines,
            now,
            TimeSpan.FromDays(options.PaymentDueDays));

        orders.Add(order);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PlacedOrderDto(order.Number, order.AccessToken);
    }

    /// <summary>
    /// 256 bitů náhody jako malé hex znaky. Ne base64: web generuje adresy malými písmeny
    /// (LowercaseUrls) a token s velkými písmeny by se cestou rozbil.
    /// </summary>
    private static string NewAccessToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private static string Text(IReadOnlyDictionary<string, string> translations, string language) =>
        translations.TryGetValue(language, out string? text) ? text : translations[DocumentLanguage];
}
