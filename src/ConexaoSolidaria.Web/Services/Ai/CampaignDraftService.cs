using ConexaoSolidaria.Web.Components.Campaigns;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ConexaoSolidaria.Web.Services.Ai;

/// <summary>Resultado da geracao assistida: payload pronto para o form + justificativa da meta.</summary>
public sealed record CampaignDraftResult(SalvarCampanha Campanha, string Justificativa);

/// <summary>
/// Criacao assistida de campanhas (Feature: Gestor ONG). A partir de um rascunho/ideia
/// livre do gestor, o agente (sem tools) devolve um <see cref="CampaignDraftSuggestion"/>
/// via structured output, que e normalizado para os limites do formulario
/// (titulo &lt;= 160, descricao &lt;= 1000, meta &gt; 0, categoria valida do seletor).
/// Falhas retornam null — a pagina exibe Snackbar e o form permanece intacto.
/// </summary>
public sealed class CampaignDraftService(
    AiChatClientProvider provider,
    ILogger<CampaignDraftService> logger)
{
    private const string Instrucoes =
        """
        Voce ajuda gestores de ONGs da plataforma Conexao Solidaria a estruturar campanhas
        de doacao. A partir da ideia enviada, produza um rascunho completo em portugues do
        Brasil: titulo mobilizador, descricao persuasiva (causa, impacto e chamado a acao)
        e uma meta de arrecadacao realista em reais.
        """;

    public const int TituloMax = 160;
    public const int DescricaoMax = 1000;
    public const decimal MetaFallback = 5000m;

    private AIAgent? _agent;

    public bool Enabled => provider.Enabled;

    /// <summary>Gera o rascunho da campanha a partir da ideia do gestor; null em falha.</summary>
    public async Task<CampaignDraftResult?> GerarAsync(string ideia, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(ideia))
        {
            return null;
        }

        try
        {
            _agent ??= provider.GetClient().AsAIAgent(new ChatClientAgentOptions
            {
                Name = "RascunhoDeCampanha",
                ChatOptions = new ChatOptions { Instructions = Instrucoes },
            });

            var resposta = await _agent.RunAsync<CampaignDraftSuggestion>(
                $"Ideia do gestor para a campanha: {ideia.Trim()}",
                cancellationToken: ct);

            return Normalizar(resposta.Result);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao gerar rascunho de campanha com IA.");
            return null;
        }
    }

    /// <summary>
    /// Normaliza a sugestao do modelo para os limites/valores validos do formulario.
    /// Publico e puro para ser testavel sem chamadas de rede.
    /// </summary>
    public static CampaignDraftResult Normalizar(CampaignDraftSuggestion sugestao)
    {
        var titulo = Truncar(sugestao.Titulo?.Trim() ?? string.Empty, TituloMax);
        var descricao = Truncar(sugestao.Descricao?.Trim() ?? string.Empty, DescricaoMax);
        var meta = sugestao.MetaSugerida > 0 ? decimal.Round(sugestao.MetaSugerida, 2) : MetaFallback;
        var categoria = NormalizarCategoria(sugestao.Categoria);

        var campanha = new SalvarCampanha(
            Titulo: titulo,
            Descricao: descricao,
            DataInicio: DateTimeOffset.Now,
            DataFim: DateTimeOffset.Now.AddDays(30),
            MetaFinanceira: meta,
            Status: "Ativa",
            Categoria: categoria);

        return new CampaignDraftResult(campanha, sugestao.Justificativa?.Trim() ?? string.Empty);
    }

    /// <summary>Casa a categoria sugerida com os valores do seletor (case-insensitive); fallback "Outros".</summary>
    public static string NormalizarCategoria(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
        {
            return "Outros";
        }

        foreach (var (value, _) in CampaignVisuals.Categorias)
        {
            if (string.Equals(value, categoria.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return value; // casing canonico esperado pelo MudSelect e pelo backend
            }
        }

        return "Outros";
    }

    private static string Truncar(string valor, int max) =>
        valor.Length <= max ? valor : valor[..max];
}
