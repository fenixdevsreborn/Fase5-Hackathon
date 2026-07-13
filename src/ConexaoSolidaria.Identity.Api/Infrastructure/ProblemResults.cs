using Microsoft.AspNetCore.Mvc;

namespace ConexaoSolidaria.Identity.Api.Infrastructure;

/// <summary>
/// Fabrica respostas padronizadas em <c>application/problem+json</c> (RFC 7807) para os
/// controllers, garantindo o content-type e o status corretos independente de content negotiation.
/// </summary>
public static class ProblemResults
{
    public static ObjectResult Validation(IDictionary<string, string[]> errors) =>
        new(new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Erro de validacao"
        })
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity,
            ContentTypes = { "application/problem+json" }
        };

    public static ObjectResult Conflict(string detail) =>
        Create(StatusCodes.Status409Conflict, "Conflito", detail);

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
