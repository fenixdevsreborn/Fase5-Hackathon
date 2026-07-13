namespace ConexaoSolidaria.Shared.Domain;

/// <summary>
/// Read model (CQRS leve) para dashboards de transparencia/gestao. E uma PROJECAO denormalizada
/// derivada de <see cref="Campaign"/> + <see cref="Donation"/>, mantida atualizada pelo
/// Donations.Worker (quem ESCREVE nesta tabela). A Campaigns.Api apenas LE (AsNoTracking) via o
/// endpoint anonimo GET /api/campanhas/stats. A escrita concentrada no Worker evita contencao no
/// caminho de leitura e mantem a transparencia eventualmente consistente com o total arrecadado.
/// </summary>
public sealed class CampaignStats
{
    public Guid CampaignId { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public decimal MetaFinanceira { get; set; }

    public decimal TotalArrecadado { get; set; }

    public int DoacoesProcessadas { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }
}
