namespace ConexaoSolidaria.Contracts.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ExchangeName { get; init; } = "conexao-solidaria";

    public string QueueName { get; init; } = "doacoes-recebidas";

    public string RoutingKey { get; init; } = "doacao.recebida";

    public string RetryExchange { get; init; } = "conexao-solidaria.retry";

    public string Retry10sQueue { get; init; } = "doacoes.retry.10s";

    public string Retry60sQueue { get; init; } = "doacoes.retry.60s";

    public int Retry10sTtlMs { get; init; } = 10000;

    public int Retry60sTtlMs { get; init; } = 60000;

    public string DeadLetterExchange { get; init; } = "conexao-solidaria.dlx";

    public string DeadLetterQueue { get; init; } = "doacoes.dead-letter";

    public string DeadLetterRoutingKey { get; init; } = "doacao.dead-letter";

    /// <summary>
    /// Exchange fanout (durable) para notificacoes best-effort de doacoes processadas. O consumidor
    /// (interface web) cria a propria fila e faz o bind; o worker apenas declara o exchange e publica.
    /// </summary>
    public string NotificationsExchange { get; init; } = "conexao-solidaria.notifications";

    public int MaxAttempts { get; init; } = 3;
}
