namespace ConexaoSolidaria.Contracts.Events;

/// <summary>
/// Notificacao best-effort publicada em um exchange fanout dedicado apos uma doacao ser processada
/// com sucesso. Consumida pela interface web (SignalR) para empurrar atualizacoes em tempo real.
/// </summary>
public sealed record DoacaoProcessadaNotification(
    Guid DoacaoId,
    Guid CampanhaId,
    string CampanhaTitulo,
    decimal Valor,
    decimal TotalArrecadado,
    decimal MetaFinanceira,
    bool MetaAtingida,
    DateTimeOffset ProcessadaEm);
