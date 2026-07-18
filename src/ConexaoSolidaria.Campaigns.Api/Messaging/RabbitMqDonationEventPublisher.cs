using System.Diagnostics;
using System.Text.Json;
using ConexaoSolidaria.Contracts.Events;
using ConexaoSolidaria.Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ConexaoSolidaria.Campaigns.Api.Messaging;

/// <summary>
/// Publisher singleton com conexao/canal reutilizaveis e publisher confirms habilitados.
/// A topologia canonica (exchange principal, filas de retry por TTL e DLX) e declarada de
/// forma idempotente na primeira publicacao. O canal e criado com tracking de confirms,
/// portanto BasicPublishAsync so retorna apos o broker confirmar (ou lanca em caso de nack).
/// </summary>
public sealed class RabbitMqDonationEventPublisher(
    IOptions<RabbitMqOptions> options,
    IConfiguration configuration,
    ILogger<RabbitMqDonationEventPublisher> logger) : IDonationEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly IConfiguration _configuration = configuration;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;
    private bool _topologyDeclared;

    public async Task PublishAsync(DoacaoRecebidaEvent donationEvent, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(donationEvent);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = donationEvent.EventoId.ToString(),
            CorrelationId = donationEvent.CorrelationId,
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>
            {
                ["traceparent"] = Activity.Current?.Id ?? donationEvent.CorrelationId,
                ["schema-version"] = donationEvent.SchemaVersion
            }
        };

        // Com publisherConfirmationTrackingEnabled, esta chamada aguarda o confirm do broker
        // e lanca (PublishException) caso a mensagem seja nack-ada ou nao roteada.
        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Evento DoacaoRecebidaEvent publicado e confirmado. EventoId={EventoId} DoacaoId={DoacaoId} CorrelationId={CorrelationId}",
            donationEvent.EventoId,
            donationEvent.DoacaoId,
            donationEvent.CorrelationId);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true } && _topologyDeclared)
        {
            return _channel;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null || !_connection.IsOpen)
            {
                var factory = RabbitMqConnectionFactoryBuilder.Build(_configuration, _options);

                _connection = await factory.CreateConnectionAsync(cancellationToken);
            }

            if (_channel is null || !_channel.IsOpen)
            {
                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);

                _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);
                _topologyDeclared = false;
            }

            if (!_topologyDeclared)
            {
                await DeclareTopologyAsync(_channel, cancellationToken);
                _topologyDeclared = true;
            }

            return _channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Declara a topologia canonica (idempotente). Os mesmos valores/args sao usados pelo Worker,
    /// portanto NAO adicionar DLX na fila principal para evitar PRECONDITION_FAILED.
    /// </summary>
    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        // Exchange e fila principal (sem args de DLX).
        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            cancellationToken: cancellationToken);

        // Retry exchange e filas com TTL que fazem dead-letter de volta para a exchange principal.
        await channel.ExchangeDeclareAsync(
            exchange: _options.RetryExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await DeclareRetryQueueAsync(
            channel,
            _options.Retry10sQueue,
            _options.Retry10sTtlMs,
            cancellationToken);

        await DeclareRetryQueueAsync(
            channel,
            _options.Retry60sQueue,
            _options.Retry60sTtlMs,
            cancellationToken);

        // Dead-letter exchange e fila final.
        await channel.ExchangeDeclareAsync(
            exchange: _options.DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.DeadLetterQueue,
            exchange: _options.DeadLetterExchange,
            routingKey: _options.DeadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }

    private async Task DeclareRetryQueueAsync(
        IChannel channel,
        string queueName,
        int ttlMs,
        CancellationToken cancellationToken)
    {
        var args = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = ttlMs,
            ["x-dead-letter-exchange"] = _options.ExchangeName,
            ["x-dead-letter-routing-key"] = _options.RoutingKey
        };

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: _options.RetryExchange,
            routingKey: queueName,
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
