namespace ConexaoSolidaria.Campaigns.Api.Responses;

public sealed record MinhaDoacaoResponse(
    Guid DoacaoId,
    Guid CampanhaId,
    string CampanhaTitulo,
    decimal ValorDoacao,
    string Status,
    DateTimeOffset CriadaEm,
    DateTimeOffset? ProcessadaEm);
