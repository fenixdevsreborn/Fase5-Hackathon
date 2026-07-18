using Microsoft.AspNetCore.Mvc;

namespace ConexaoSolidaria.Campaigns.Api.Infrastructure;

/// <summary>
/// Fabrica respostas padronizadas em <c>application/problem+json</c> (RFC 7807) para os
/// controllers, garantindo o content-type e o status corretos independente de content negotiation.
/// </summary>
public static class ProblemResults
{
    public static ObjectResult UnprocessableEntity(string detail) =>
        Create(StatusCodes.Status422UnprocessableEntity, "Requisicao nao processavel", detail);

    public static ObjectResult NotFound(string detail) =>
        Create(StatusCodes.Status404NotFound, "Recurso nao encontrado", detail);

    public static ObjectResult Unauthorized(string detail) =>
        Create(StatusCodes.Status401Unauthorized, "Nao autenticado", detail);

    private static ObjectResult Create(int statusCode, string title, string detail) =>
        new(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        })
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
}
