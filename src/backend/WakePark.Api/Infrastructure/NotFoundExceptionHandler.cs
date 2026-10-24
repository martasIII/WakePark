using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using WakePark.BuildingBlocks.Application;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Záznam, který neexistuje nebo patří jinému provozovateli, je 404 — v obou případech stejně,
/// aby z odpovědi nešlo poznat, že cizí záznam existuje (ADR 0007).
/// </summary>
internal sealed partial class NotFoundExceptionHandler(ILogger<NotFoundExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not NotFoundException notFound)
        {
            return false;
        }

        LogNotFound(logger, notFound.Message);

        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Nenalezeno",
            Detail = notFound.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        };

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Nenalezeno: {Reason}")]
    private static partial void LogNotFound(ILogger logger, string reason);
}
