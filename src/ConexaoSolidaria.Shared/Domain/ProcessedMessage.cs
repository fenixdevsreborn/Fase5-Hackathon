namespace ConexaoSolidaria.Shared.Domain;

/// <summary>
/// Registro de idempotencia: cada evento processado (por <see cref="EventId"/>) e persistido uma unica vez.
/// A chave primaria garante deduplicacao mesmo em redelivery concorrente.
/// </summary>
public sealed class ProcessedMessage
{
    public Guid EventId { get; init; }

    public DateTimeOffset ProcessedAtUtc { get; init; }
}
