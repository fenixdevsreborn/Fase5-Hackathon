using System.Text.Json;
using ConexaoSolidaria.Contracts.Events;
using ConexaoSolidaria.Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConexaoSolidaria.Web.Services;

/// <summary>
/// Servico hospedado (singleton) que consome o exchange fanout de notificacoes de doacoes
/// processadas e as reemite, via <see cref="NotificationDispatcher"/>, para as telas Blazor
/// conectadas — dando atualizacao em tempo real por cima do polling de status.
///
/// RESILIENCIA (critica): a interface web NAO pode depender do RabbitMQ para funcionar. Se a
/// configuracao estiver ausente ou a conexao/consumo falhar, este servico apenas registra um
/// warning e tenta reconectar com backoff exponencial (limitado), em loop, sem nunca lancar para
/// fora do <see cref="ExecuteAsync"/>. Enquanto o broker estiver indisponivel, o
/// <c>DonationStatus</c> continua funcionando pelo seu polling (fallback).
///
/// Topologia: declara o <see cref="RabbitMqOptions.NotificationsExchange"/> (fanout, durable) de
/// forma idempotente — mesmos parametros do Worker — e cria uma fila anonima exclusiva/auto-delete
/// (nome gerado pelo servidor) que some quando este consumidor cai. Cada replica web tem a propria
/// fila, entao todas as replicas recebem uma copia de cada notificacao (fanout).
/// </summary>
public sealed class NotificationConsumer(
    NotificationDispatcher dispatcher,
    IOptions<RabbitMqOptions> options,
    IConfiguration configuration,
    ILogger<NotificationConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = MinBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumerAsync(stoppingToken);

                // RunConsumerAsync so retorna se o loop de espera terminar (cancelamento). Reseta o
                // backoff para a proxima iteracao caso tenha havido um ciclo saudavel.
                backoff = MinBackoff;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // NAO derruba a app: apenas loga e tenta reconectar depois. O polling do
                // DonationStatus permanece como fallback enquanto o broker estiver fora.
                logger.LogWarning(
                    ex,
                    "Consumidor de notificacoes indisponivel (RabbitMQ). Tentando reconectar em {Backoff}s. As telas continuam com o polling de status como fallback.",
                    backoff.TotalSeconds);

                try
                {
                    await Task.Delay(backoff, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                // Backoff exponencial limitado para nao martelar um broker fora do ar.
                backoff = TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, backoff.TotalSeconds * 2));
            }
        }
    }

    private async Task RunConsumerAsync(CancellationToken stoppingToken)
    {
        var factory = RabbitMqConnectionFactoryBuilder.Build(configuration, _options);

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Exchange fanout durable (idempotente; mesmos parametros do Worker/publisher).
        await channel.ExchangeDeclareAsync(
            exchange: _options.NotificationsExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Fila anonima: nome gerado pelo servidor, exclusive (so este canal) e auto-delete (some
        // quando o consumidor cai). Assim cada replica web tem a propria fila efemera e todas
        // recebem uma copia de cada notificacao pelo fanout.
        var queue = await channel.QueueDeclareAsync(cancellationToken: stoppingToken);
        var queueName = queue.QueueName;

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: _options.NotificationsExchange,
            routingKey: string.Empty,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, args) => HandleMessageAsync(args, stoppingToken);

        // autoAck: true — notificacoes sao best-effort/descartaveis; se a web cair, a fila
        // auto-delete some e nao ha nada a reprocessar (o estado real vem do backend via polling).
        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Consumidor de notificacoes conectado. Fila anonima {QueueName} vinculada ao exchange {Exchange}.",
            queueName,
            _options.NotificationsExchange);

        // Mantem o consumidor vivo ate o cancelamento (ou ate a conexao cair, o que lanca e
        // dispara a reconexao com backoff no ExecuteAsync).
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private Task HandleMessageAsync(BasicDeliverEventArgs args, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        try
        {
            var notification = JsonSerializer.Deserialize<DoacaoProcessadaNotification>(args.Body.Span);
            if (notification is not null)
            {
                dispatcher.Publish(notification);
            }
            else
            {
                logger.LogWarning("Notificacao de doacao processada vazia ou nula. Ignorando.");
            }
        }
        catch (JsonException ex)
        {
            // Mensagem malformada nunca vira util: apenas loga e ignora (autoAck ja a removeu).
            logger.LogWarning(ex, "Notificacao de doacao processada com JSON invalido. Ignorando.");
        }
        catch (Exception ex)
        {
            // Nunca propaga: uma falha ao entregar uma notificacao nao pode derrubar o consumidor.
            logger.LogWarning(ex, "Falha ao processar notificacao de doacao processada (best-effort).");
        }

        return Task.CompletedTask;
    }
}
