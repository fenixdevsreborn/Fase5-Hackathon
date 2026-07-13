namespace ConexaoSolidaria.Contracts.Events;

public sealed record DoacaoRecebidaEvent(
    Guid EventoId,
    Guid DoacaoId,
    Guid CampanhaId,
    Guid DoadorId,
    string DoadorEmail,
    decimal ValorDoacao,
    DateTimeOffset OcorridaEm,
    string CorrelationId = "",
    int SchemaVersion = 1);
