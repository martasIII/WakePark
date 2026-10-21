using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Neplatný vstup (FluentValidation) je chyba klienta — 400 se seznamem chyb podle RFC 9457.
/// Web z něj zákazníkovi vypíše, co má opravit; bez handleru by to skončilo jako 500.
/// </summary>
internal sealed partial class ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validation)
        {
            return false;
        }

        LogValidationFailed(logger, validation.Message);

        // Chyba bez konkrétního pole (např. „produkt už není v nabídce“) jde pod prázdný klíč.
        Dictionary<string, string[]> errors = validation.Errors.Any()
            ? validation.Errors
                .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal)
            : new Dictionary<string, string[]>(StringComparer.Ordinal) { [string.Empty] = [validation.Message] };

        ValidationProblemDetails problem = new(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Neplatný požadavek",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Neplatný vstup: {Reason}")]
    private static partial void LogValidationFailed(ILogger logger, string reason);
}
