namespace ConexaoSolidaria.Campaigns.Api.Responses;

/// <summary>
/// Projecao de leitura (read model) para dashboards. A tabela de origem (campaign_stats) e ESCRITA
/// pelo Donations.Worker; a Campaigns.Api apenas le.
/// </summary>
public sealed record CampanhaStatsResponse(
    Guid CampaignId,
    string Titulo,
    decimal MetaFinanceira,
    decimal TotalArrecadado,
    int DoacoesProcessadas,
    DateTimeOffset AtualizadoEm);
