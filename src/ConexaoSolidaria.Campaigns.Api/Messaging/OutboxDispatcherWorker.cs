using System.Text.Json;
using ConexaoSolidaria.Shared.Domain;
using ConexaoSolidaria.Contracts.Events;
using ConexaoSolidaria.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Prometheus;

namespace ConexaoSolidaria.Campaigns.Api.Messaging;

/// <summary>
/// Dispatcher em background do padrao Outbox. Varre periodicamente as mensagens pendentes,
/// publica no broker (com publisher confirms) e marca como publicadas. Em falha aplica backoff
/// incremental para nova tentativa, garantindo entrega ao menos uma vez (at-least-once).
/// </summary>
public sealed class OutboxDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    IDonationEventPublisher publisher,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private static readonly Gauge PendingMessages = Metrics.CreateGauge(
        "conexao_outbox_pending_messages",
        "Quantidade de mensagens pendentes na outbox aguardando publicacao.");

    private static readonly Counter PublishSuccess = Metrics.CreateCounter(
        "conexao_donation_publish_total",
        "Quantidade de eventos de doacao publicados com sucesso no broker.");

    private static readonly Counter PublishFailures = Metrics.CreateCounter(
        "conexao_donation_publish_failures_total",
        "Quantidade de falhas ao publicar eventos de doacao no broker.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha inesperada no ciclo do OutboxDispatcherWorker.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();

        var now = DateTimeOffset.UtcNow;

        var pendingCount = await db.OutboxMessages
            .Where(message => message.PublishedAtUtc == null)
            .CountAsync(cancellationToken);

        PendingMessages.Set(pendingCount);

        if (pendingCount == 0)
        {
            return;
        }

        var messages = await db.OutboxMessages
            .Where(message => message.PublishedAtUtc == null
                && (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= now))
            .OrderBy(message => message.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            await DispatchAsync(message, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var donationEvent = JsonSerializer.Deserialize<DoacaoRecebidaEvent>(message.Payload)
                ?? throw new InvalidOperationException("Payload da outbox nao pode ser desserializado para DoacaoRecebidaEvent.");

            await publisher.PublishAsync(donationEvent, cancellationToken);

            message.MarkAsPublished(DateTimeOffset.UtcNow);
            PublishSuccess.Inc();
        }
        catch (Exception ex)
        {
            PublishFailures.Inc();

            var attemptsAfterFailure = message.Attempts + 1;
            var backoff = TimeSpan.FromSeconds(Math.Min(60, 5 * attemptsAfterFailure));
            message.RegisterFailure(ex.Message, DateTimeOffset.UtcNow + backoff);

            logger.LogWarning(
                ex,
                "Falha ao publicar mensagem da outbox. OutboxId={OutboxId} Attempts={Attempts} ProximaTentativaEm={NextAttempt}",
                message.Id,
                attemptsAfterFailure,
                message.NextAttemptAtUtc);
        }
    }
}
