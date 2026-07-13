namespace ConexaoSolidaria.Shared.Domain;

public sealed class Donation
{
    private Donation()
    {
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CampaignId { get; private set; }

    public Campaign? Campaign { get; private set; }

    public Guid DoadorId { get; private set; }

    public string DoadorEmail { get; private set; } = string.Empty;

    public decimal Valor { get; private set; }

    public DonationStatus Status { get; private set; } = DonationStatus.Pendente;

    public DateTimeOffset CriadaEm { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessadaEm { get; private set; }

    public static Donation Create(Guid campaignId, Guid doadorId, string doadorEmail, decimal valor)
    {
        if (valor <= 0)
        {
            throw new DomainRuleException("ValorDoacao deve ser maior que zero.");
        }

        return new Donation
        {
            CampaignId = campaignId,
            DoadorId = doadorId,
            DoadorEmail = doadorEmail.Trim().ToLowerInvariant(),
            Valor = valor
        };
    }

    public void MarkAsProcessed(DateTimeOffset processedAt)
    {
        EnsurePendente();
        Status = DonationStatus.Processada;
        ProcessadaEm = processedAt.ToUniversalTime();
    }

    public void MarkAsRejected(DateTimeOffset processedAt)
    {
        EnsurePendente();
        Status = DonationStatus.Rejeitada;
        ProcessadaEm = processedAt.ToUniversalTime();
    }

    /// <summary>
    /// Marca a doacao como Falha (Pendente -> Falha) em caso de erro tecnico no processamento.
    /// Diferente de <see cref="MarkAsRejected"/>, que e uma decisao de negocio definitiva.
    /// </summary>
    public void MarkAsFailed(DateTimeOffset processedAt)
    {
        EnsurePendente();
        Status = DonationStatus.Falha;
        ProcessadaEm = processedAt.ToUniversalTime();
    }

    /// <summary>
    /// Reprocessamento administrativo: retorna uma doacao em Falha para Pendente, permitindo
    /// uma nova tentativa de processamento. Unica transicao de saida de Falha (Falha -> Pendente).
    /// So e permitida a partir de <see cref="DonationStatus.Falha"/>.
    /// </summary>
    public void RetryAfterFailure()
    {
        if (Status != DonationStatus.Falha)
        {
            throw new DomainRuleException(
                $"Retry so e permitido para doacao em Falha; status atual {Status}.");
        }

        Status = DonationStatus.Pendente;
        ProcessadaEm = null;
    }

    // Garante a transicao unica de estado: uma doacao finalizada (Processada ou Rejeitada) ou em
    // Falha nao pode transicionar diretamente de novo por este guard. Reforca a idempotencia no
    // nivel de dominio, alem do guard de status/dedup ja aplicado no worker. Falha so retorna a
    // Pendente pela transicao explicita de retry administrativo (RetryAfterFailure).
    private void EnsurePendente()
    {
        if (Status != DonationStatus.Pendente)
        {
            throw new DomainRuleException(
                $"Doacao ja finalizada com status {Status}; transicao de estado nao permitida.");
        }
    }
}
