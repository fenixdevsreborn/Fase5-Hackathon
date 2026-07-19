namespace ConexaoSolidaria.Campaigns.Api.Responses;

public sealed record TransparenciaCampanhaResponse(
    Guid Id,
    string Titulo,
    string Descricao,
    DateTimeOffset DataInicio,
    DateTimeOffset DataFim,
    decimal MetaFinanceira,
    decimal ValorTotalArrecadado);
