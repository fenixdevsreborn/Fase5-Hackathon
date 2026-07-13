namespace ConexaoSolidaria.Campaigns.Api.Responses;

public sealed record DoacaoStatusResponse(
    Guid DoacaoId,
    Guid CampanhaId,
    decimal ValorDoacao,
    string Status,
    string CampanhaTitulo,
    DateTimeOffset CriadaEm,
    DateTimeOffset? ProcessadaEm);
