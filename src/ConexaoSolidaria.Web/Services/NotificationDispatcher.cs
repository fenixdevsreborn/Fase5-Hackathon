using ConexaoSolidaria.Contracts.Events;

namespace ConexaoSolidaria.Web.Services;

/// <summary>
/// Ponto central em memoria (singleton) que reemite as notificacoes de doacao processada
/// recebidas do RabbitMQ para as telas Blazor conectadas via o circuito SignalR.
///
/// O <see cref="NotificationConsumer"/> (BackgroundService) chama <see cref="Publish"/> a cada
/// mensagem; os componentes (DonationStatus, MainLayout) assinam <see cref="Received"/> e fazem
/// <c>InvokeAsync(StateHasChanged)</c> ao serem notificados. O evento e disparado em uma thread
/// do consumidor RabbitMQ — NAO na thread do circuito — por isso os assinantes DEVEM usar
/// <c>InvokeAsync</c> para voltar ao contexto de renderizacao.
///
/// Thread-safe: a lista de assinantes e protegida por lock e o disparo ocorre sobre um snapshot
/// imutavel, de modo que assinar/desassinar durante a entrega nunca lanca nem perde handlers.
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly object _gate = new();
    private Action<DoacaoProcessadaNotification>? _received;

    /// <summary>
    /// Disparado a cada notificacao de doacao processada. Assinantes rodam FORA da thread do
    /// circuito Blazor; use <c>InvokeAsync(StateHasChanged)</c> ao reagir. Sempre desassine no
    /// <c>Dispose</c> do componente para evitar vazamento de circuitos encerrados.
    /// </summary>
    public event Action<DoacaoProcessadaNotification>? Received
    {
        add
        {
            lock (_gate)
            {
                _received += value;
            }
        }
        remove
        {
            lock (_gate)
            {
                _received -= value;
            }
        }
    }

    /// <summary>
    /// Reemite <paramref name="notification"/> a todos os assinantes atuais. Cada handler e isolado:
    /// uma excecao em um assinante e engolida para nao impedir a entrega aos demais nem derrubar o
    /// consumidor. Chamado pelo <see cref="NotificationConsumer"/> a cada mensagem do fanout.
    /// </summary>
    public void Publish(DoacaoProcessadaNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Action<DoacaoProcessadaNotification>? handlers;
        lock (_gate)
        {
            handlers = _received;
        }

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Action<DoacaoProcessadaNotification>>())
        {
            try
            {
                handler(notification);
            }
            catch
            {
                // Best-effort: um assinante com falha (ex.: circuito ja encerrado) nao pode impedir
                // a entrega aos demais nem derrubar o BackgroundService que faz este Publish.
            }
        }
    }
}
