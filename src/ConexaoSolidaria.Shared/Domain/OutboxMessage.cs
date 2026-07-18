namespace ConexaoSolidaria.Shared.Domain;

/// <summary>
/// Mensagem persistida no padrao Outbox transacional. Gravada na mesma transacao (SaveChanges)
/// que a alteracao de negocio e publicada de forma assincrona pelo OutboxDispatcherWorker.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public int SchemaVersion { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public static OutboxMessage Create(string eventType, int schemaVersion, string payload, string correlationId)
    {
        var now = DateTimeOffset.UtcNow;

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            EventType = eventType,
            SchemaVersion = schemaVersion,
            Payload = payload,
            OccurredAtUtc = now,
            PublishedAtUtc = null,
            Attempts = 0,
            NextAttemptAtUtc = now,
            LastError = null,
            CorrelationId = correlationId
        };
    }

    public void MarkAsPublished(DateTimeOffset publishedAtUtc)
    {
        PublishedAtUtc = publishedAtUtc;
        NextAttemptAtUtc = null;
        LastError = null;
    }

    public void RegisterFailure(string error, DateTimeOffset nextAttemptAtUtc)
    {
        Attempts += 1;
        LastError = error;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }
}
