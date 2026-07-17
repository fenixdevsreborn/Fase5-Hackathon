using ConexaoSolidaria.Web.Services.Ai;
using Microsoft.Extensions.AI;

namespace ConexaoSolidaria.Tests.Ai;

public sealed class AiOptionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("sk-teste", true)]
    public void IsConfigured_DeveExigirChaveNaoVazia(string? apiKey, bool esperado)
    {
        var options = new AiOptions { ApiKey = apiKey };

        Assert.Equal(esperado, options.IsConfigured);
    }

    [Fact]
    public void Defaults_DevemSerModeloBaratoComLimiteDeTokens()
    {
        var options = new AiOptions();

        Assert.Equal("gpt-4o-mini", options.Model);
        Assert.Equal(800, options.MaxOutputTokens);
        Assert.False(options.IsConfigured);
    }

    /// <summary>
    /// Protege o structured output da criacao assistida: um rename em
    /// CampaignDraftSuggestion quebraria o JSON schema enviado ao modelo.
    /// </summary>
    [Fact]
    public void SchemaDoRascunho_DeveConterAsCincoPropriedades()
    {
        var schema = AIJsonUtilities.CreateJsonSchema(typeof(CampaignDraftSuggestion)).ToString();

        Assert.Contains("titulo", schema);
        Assert.Contains("descricao", schema);
        Assert.Contains("categoria", schema);
        Assert.Contains("metaSugerida", schema);
        Assert.Contains("justificativa", schema);
    }
}
