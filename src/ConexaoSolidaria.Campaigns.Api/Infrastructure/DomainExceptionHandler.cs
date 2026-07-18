using ConexaoSolidaria.Shared.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ConexaoSolidaria.Campaigns.Api.Infrastructure;

/// <summary>
/// Traduz <see cref="DomainRuleException"/> (violacao de regra de negocio do dominio) em uma
/// resposta 422 Unprocessable Entity com corpo <c>application/problem+json</c>. Demais excecoes
/// nao sao tratadas aqui (retorna false) e caem no ProblemDetails padrao (500).
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainRuleException domainRuleException)
        {
            return false;
        }

        logger.LogWarning(domainRuleException, "Regra de negocio violada: {Message}", domainRuleException.Message);

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = domainRuleException,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Regra de negocio violada",
                Detail = domainRuleException.Message,
                Type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2"
            }
        });
    }
}
