using ConexaoSolidaria.Web.Services.Ai;

namespace ConexaoSolidaria.Tests.Ai;

/// <summary>
/// Testes da normalizacao do structured output da criacao assistida de campanhas.
/// Nao ha chamada de rede: Normalizar/NormalizarCategoria sao puros.
/// </summary>
public sealed class CampaignDraftServiceTests
{
    private static CampaignDraftSuggestion Sugestao(
        string titulo = "Ajude o abrigo",
        string descricao = "Campanha para ajudar animais resgatados.",
        string categoria = "Animais",
        decimal meta = 1500m,
        string justificativa = "Custo mensal do abrigo.") =>
        new(titulo, descricao, categoria, meta, justificativa);

    [Theory]
    [InlineData("Animais", "Animais")]
    [InlineData("animais", "Animais")]          // casing normalizado para o valor canonico
    [InlineData("MEIOAMBIENTE", "MeioAmbiente")]
    [InlineData("  Saude  ", "Saude")]          // espacos ignorados
    [InlineData("Gastronomia", "Outros")]       // categoria inexistente -> fallback
    [InlineData("", "Outros")]
    [InlineData(null, "Outros")]
    public void NormalizarCategoria_DeveCasarComSeletorOuCairEmOutros(string? entrada, string esperado)
    {
        Assert.Equal(esperado, CampaignDraftService.NormalizarCategoria(entrada));
    }

    [Fact]
    public void Normalizar_DeveTruncarTituloEDescricaoNosLimitesDoFormulario()
    {
        var sugestao = Sugestao(
            titulo: new string('t', 500),
            descricao: new string('d', 5000));

        var resultado = CampaignDraftService.Normalizar(sugestao);

        Assert.Equal(CampaignDraftService.TituloMax, resultado.Campanha.Titulo.Length);
        Assert.Equal(CampaignDraftService.DescricaoMax, resultado.Campanha.Descricao.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Normalizar_MetaInvalida_DeveUsarFallback(decimal meta)
    {
        var resultado = CampaignDraftService.Normalizar(Sugestao(meta: meta));

        Assert.Equal(CampaignDraftService.MetaFallback, resultado.Campanha.MetaFinanceira);
    }

    [Fact]
    public void Normalizar_DeveArredondarMetaParaDuasCasas()
    {
        var resultado = CampaignDraftService.Normalizar(Sugestao(meta: 1234.5678m));

        Assert.Equal(1234.57m, resultado.Campanha.MetaFinanceira);
    }

    [Fact]
    public void Normalizar_DevePreencherDefaultsDoFluxoDeCriacao()
    {
        var resultado = CampaignDraftService.Normalizar(Sugestao());

        Assert.Equal("Ativa", resultado.Campanha.Status);
        Assert.Equal("Animais", resultado.Campanha.Categoria);
        Assert.Equal("Custo mensal do abrigo.", resultado.Justificativa);
        // Janela default de 30 dias, igual ao form manual (NovaCampanha).
        var dias = (resultado.Campanha.DataFim - resultado.Campanha.DataInicio).TotalDays;
        Assert.Equal(30, dias, precision: 0);
    }
}
