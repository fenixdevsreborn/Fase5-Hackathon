namespace ConexaoSolidaria.Shared.Domain;

/// <summary>
/// Registra uma chave de idempotencia (header <c>Idempotency-Key</c>) associada a uma doacao ja
/// aceita. Gravada na MESMA transacao (SaveChanges) que a doacao e a OutboxMessage, garante que
/// uma reapresentacao da mesma requisicao retorne a doacao original sem criar duplicata.
/// </summary>
public sealed class DonationIdempotencyKey
{
    private DonationIdempotencyKey()
    {
    }

    public string Key { get; private set; } = string.Empty;

    public Guid DonationId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static DonationIdempotencyKey Create(string key, Guid donationId)
    {
        return new DonationIdempotencyKey
        {
            Key = key,
            DonationId = donationId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
