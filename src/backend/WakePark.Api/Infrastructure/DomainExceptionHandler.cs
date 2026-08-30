using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using WakePark.BuildingBlocks.Domain;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Porušení doménového pravidla není chyba serveru. Překládá se na 422 s tělem podle RFC 9457,
/// aby klient poznal rozdíl mezi "zadal jsi něco nepřípustného" a "spadli jsme".
/// </summary>
internal sealed partial class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        LogDomainRuleViolated(logger, domainException.Code, domainException);

        // Neplatné přihlašovací údaje nejsou porušené doménové pravidlo, ale odepřený
        // přístup. Klient na ně reaguje jinak, proto jim patří vlastní stavový kód.
        int status = string.Equals(domainException.Code, "auth.invalid_credentials", StringComparison.Ordinal)
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status422UnprocessableEntity;

        ProblemDetails problem = new()
        {
            Status = status,
            Title = status == StatusCodes.Status401Unauthorized
                ? "Přihlášení se nezdařilo"
                : "Porušeno doménové pravidlo",
            Detail = domainException.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
        };

        if (domainException.Code is not null)
        {
            problem.Extensions["code"] = domainException.Code;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Porušeno doménové pravidlo {DomainErrorCode}.")]
    private static partial void LogDomainRuleViolated(
        ILogger logger,
        string? domainErrorCode,
        Exception exception);
}
