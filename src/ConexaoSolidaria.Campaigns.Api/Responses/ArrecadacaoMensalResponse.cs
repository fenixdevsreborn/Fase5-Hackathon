namespace ConexaoSolidaria.Campaigns.Api.Responses;

/// <summary>
/// Um mes da serie historica de arrecadacao: soma das doacoes PROCESSADAS cuja data de
/// processamento cai no mes, no fuso de referencia do relatorio (nao em UTC — ver
/// CampanhasController.ArrecadacaoMensal).
///
/// A serie vem completa: meses sem doacao aparecem com <see cref="Total"/> zero, para o
/// grafico nao encolher o eixo do tempo e sugerir uma continuidade que nao existe.
/// </summary>
public sealed record ArrecadacaoMensalResponse(
    int Ano,
    int Mes,
    decimal Total,
    long Doacoes);
