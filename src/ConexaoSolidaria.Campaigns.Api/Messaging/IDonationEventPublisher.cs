using ConexaoSolidaria.Contracts.Events;

namespace ConexaoSolidaria.Campaigns.Api.Messaging;

public interface IDonationEventPublisher
{
    /// <summary>
    /// Publica o evento no broker aguardando o publisher confirm.
    /// Lanca excecao caso a publicacao nao seja confirmada (nack/timeout/erro de conexao).
    /// </summary>
    Task PublishAsync(DoacaoRecebidaEvent donationEvent, CancellationToken cancellationToken);
}
