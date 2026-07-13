namespace ConexaoSolidaria.Shared.Domain;

public enum DonationStatus
{
    Pendente = 1,
    Processada = 2,
    Rejeitada = 3,

    /// <summary>
    /// Falha tecnica no processamento (ex.: erro transitorio/infra). Diferente de Rejeitada,
    /// que representa uma decisao de negocio definitiva. Uma doacao em Falha so pode voltar a
    /// Pendente por retry administrativo explicito (ver <see cref="Donation.RetryAfterFailure"/>).
    /// </summary>
    Falha = 4
}
