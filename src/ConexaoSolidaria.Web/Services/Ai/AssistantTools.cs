using System.ComponentModel;
using System.Text.Json;

namespace ConexaoSolidaria.Web.Services.Ai;

/// <summary>
/// Function tools do Assistente Solidario. Scoped: delega ao <see cref="ApiClient"/> do
/// mesmo circuito Blazor, que anexa o Bearer do <see cref="TokenProvider"/> automaticamente
/// (nenhum plumbing extra de JWT).
///
/// Convencoes:
/// - Retornam JSON compacto de projecoes pequenas (nunca DTOs/paginas cruas) para
///   economizar tokens do modelo.
/// - Nunca lancam por estado esperado (sem login, lista vazia): devolvem uma frase que o
///   modelo transforma em resposta educada.
/// </summary>
public sealed class AssistantTools(ApiClient api, TokenProvider tokenProvider)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Description("Busca campanhas de doacao ativas por termo livre (titulo/descricao/causa). Retorna ate 5 campanhas com id, titulo, categoria, meta e valor arrecadado.")]
    public async Task<string> BuscarCampanhasAsync(
        [Description("Termo de busca, ex.: 'saude', 'criancas', 'racao para caes'. Vazio lista as campanhas mais relevantes.")] string? termo = null,
        [Description("Pagina de resultados, comecando em 1.")] int pagina = 1,
        CancellationToken ct = default)
    {
        var page = await api.BuscarCampanhasAsync(termo, Math.Max(1, pagina), pageSize: 5, ct);
        if (page.Items.Count == 0)
        {
            return "Nenhuma campanha encontrada para esse termo. Sugira ao usuario tentar outro termo ou listar todas.";
        }

        var itens = page.Items.Select(c => new
        {
            id = c.Id,
            titulo = c.Titulo,
            categoria = c.Categoria,
            status = c.Status,
            metaFinanceira = c.MetaFinanceira,
            valorArrecadado = c.ValorTotalArrecadado,
            terminaEm = c.DataFim.ToString("yyyy-MM-dd"),
        });

        return JsonSerializer.Serialize(new { totalEncontradas = page.Total, campanhas = itens }, Json);
    }

    [Description("Obtem os detalhes completos de uma campanha especifica pelo id (GUID) retornado pela busca.")]
    public async Task<string> ObterCampanhaAsync(
        [Description("Id (GUID) da campanha.")] Guid campanhaId,
        CancellationToken ct = default)
    {
        var c = await api.ObterCampanhaAsync(campanhaId, ct);
        if (c is null)
        {
            return "Campanha nao encontrada. Confirme o id usando a busca de campanhas.";
        }

        return JsonSerializer.Serialize(new
        {
            id = c.Id,
            titulo = c.Titulo,
            descricao = c.Descricao,
            categoria = c.Categoria,
            status = c.Status,
            metaFinanceira = c.MetaFinanceira,
            valorArrecadado = c.ValorTotalArrecadado,
            totalDoadores = c.TotalDoadores,
            inicio = c.DataInicio.ToString("yyyy-MM-dd"),
            fim = c.DataFim.ToString("yyyy-MM-dd"),
        }, Json);
    }

    [Description("Consulta as estatisticas publicas de transparencia: total arrecadado, meta e numero de doacoes processadas por campanha.")]
    public async Task<string> ObterEstatisticasAsync(CancellationToken ct = default)
    {
        var stats = await api.StatsCampanhasAsync(ct);
        if (stats.Count == 0)
        {
            // O read model e populado pelo worker; sem doacoes processadas ainda, cai na vitrine.
            var vitrine = await api.TransparenciaAsync(ct);
            if (vitrine.Count == 0)
            {
                return "Ainda nao ha dados de arrecadacao disponiveis.";
            }

            return JsonSerializer.Serialize(vitrine.Select(t => new
            {
                titulo = t.Titulo,
                metaFinanceira = t.MetaFinanceira,
                valorArrecadado = t.ValorTotalArrecadado,
            }), Json);
        }

        return JsonSerializer.Serialize(stats.Select(s => new
        {
            titulo = s.Titulo,
            metaFinanceira = s.MetaFinanceira,
            totalArrecadado = s.TotalArrecadado,
            doacoesProcessadas = s.DoacoesProcessadas,
            atualizadoEm = s.AtualizadoEm.ToString("yyyy-MM-dd HH:mm"),
        }), Json);
    }

    [Description("Lista as doacoes do usuario logado com status de processamento (Pendente/Processada/Rejeitada). Exige que o usuario esteja autenticado como doador.")]
    public async Task<string> MinhasDoacoesAsync(CancellationToken ct = default)
    {
        if (!tokenProvider.HasToken)
        {
            return "O usuario nao esta logado. Peca educadamente para ele entrar na conta (pagina /entrar) para consultar as doacoes.";
        }

        var doacoes = await api.MinhasDoacoesAsync(ct);
        if (doacoes.Count == 0)
        {
            return "O usuario ainda nao fez nenhuma doacao. Sugira conhecer as campanhas ativas.";
        }

        return JsonSerializer.Serialize(doacoes.Select(d => new
        {
            campanha = d.CampanhaTitulo,
            valor = d.ValorDoacao,
            status = d.Status,
            criadaEm = d.CriadaEm.ToString("yyyy-MM-dd HH:mm"),
            processadaEm = d.ProcessadaEm?.ToString("yyyy-MM-dd HH:mm"),
        }), Json);
    }
}
