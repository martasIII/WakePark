using Microsoft.AspNetCore.OpenApi;

using Microsoft.OpenApi;

namespace WakePark.Api.OpenApi;

/// <summary>
/// Doplní hlavičku OpenAPI dokumentu. Bez toho se dokumentace jmenuje po assembly
/// a nikde není napsáno, k čemu vlastně je.
/// </summary>
internal sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info = new OpenApiInfo
        {
            Title = "WakePark API",
            Version = "v1",
            Description = """
                Rezervační, billingový a informační systém pro wakeboardingové parky.

                **Čas** — všechny okamžiky jsou v UTC. Datum v parametru `date` se naopak zadává
                v lokálním čase areálu, protože provozní doba je vedená lokálně.

                **Chyby** — odpovědi podle RFC 9457 (`application/problem+json`).
                Porušení doménového pravidla vrací **422** a v poli `code` strojově čitelný
                identifikátor, například `slot.capacity_exceeded`. To je něco jiného než **400**,
                které znamená vadný požadavek, a než **409** při souběžné změně.
                """,
        };

        return Task.CompletedTask;
    }
}
