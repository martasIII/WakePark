using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Nečitelné tělo požadavku je chyba klienta, ne pád serveru. Minimal API na ni vyhodí
/// <see cref="BadHttpRequestException"/>, která by jinak skončila jako HTTP 500 a v logu
/// jako "unhandled exception" — a při hledání skutečných chyb by jen dělala hluk.
/// </summary>
internal sealed partial class BadRequestExceptionHandler(ILogger<BadRequestExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        LogBadRequest(logger, badRequest.Message);

        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Neplatný požadavek",
            Detail = badRequest.InnerException?.Message ?? badRequest.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Klient poslal neplatný požadavek: {Reason}")]
    private static partial void LogBadRequest(ILogger logger, string reason);
}
