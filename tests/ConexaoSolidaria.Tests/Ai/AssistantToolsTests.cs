using System.Net;
using System.Text;
using System.Text.Json;
using ConexaoSolidaria.Web.Services;
using ConexaoSolidaria.Web.Services.Ai;

namespace ConexaoSolidaria.Tests.Ai;

/// <summary>
/// Testes dos function tools do Assistente Solidario. O ApiClient real roda sobre um
/// HttpMessageHandler fake (sem rede, sem OpenAI) — valida o contrato dos tools:
/// JSON compacto, mensagens de estado (sem login / vazio) e o guard de autenticacao.
/// </summary>
public sealed class AssistantToolsTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private static HttpResponseMessage Json(string payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

    private static (AssistantTools Tools, StubHandler Handler, TokenProvider TokenProvider) CriarTools(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://gateway") };
        var tokenProvider = new TokenProvider();
        var api = new ApiClient(http, tokenProvider);
        return (new AssistantTools(api, tokenProvider), handler, tokenProvider);
    }

    [Fact]
    public async Task MinhasDoacoes_SemLogin_DeveOrientarSemChamarApi()
    {
        var (tools, handler, _) = CriarTools(_ => Json("[]"));

        var resposta = await tools.MinhasDoacoesAsync();

        Assert.Contains("nao esta logado", resposta);
        Assert.Empty(handler.Requests); // guard: nenhuma chamada HTTP sem token
    }

    [Fact]
    public async Task MinhasDoacoes_ComLogin_DeveSerializarProjecaoCompacta()
    {
        var (tools, handler, tokenProvider) = CriarTools(_ => Json(
            """
            [{
                "doacaoId": "0d9a04a4-0000-0000-0000-000000000001",
                "campanhaId": "0d9a04a4-0000-0000-0000-000000000002",
                "campanhaTitulo": "Agasalho 2026",
                "valorDoacao": 50.0,
                "status": "Processada",
                "criadaEm": "2026-07-01T12:00:00Z",
                "processadaEm": "2026-07-01T12:00:05Z"
            }]
            """));
        tokenProvider.Token = "jwt-de-teste";

        var resposta = await tools.MinhasDoacoesAsync();

        using var json = JsonDocument.Parse(resposta);
        var item = json.RootElement[0];
        Assert.Equal("Agasalho 2026", item.GetProperty("campanha").GetString());
        Assert.Equal("Processada", item.GetProperty("status").GetString());
        // O Bearer do TokenProvider foi propagado pelo ApiClient (mesmo caminho do app).
        Assert.Equal("jwt-de-teste", handler.Requests.Single().Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task BuscarCampanhas_SemResultados_DeveRetornarMensagemDeVazio()
    {
        var (tools, _, _) = CriarTools(_ => Json(
            """{ "items": [], "page": 1, "pageSize": 5, "total": 0, "totalPages": 0 }"""));

        var resposta = await tools.BuscarCampanhasAsync("termo-sem-resultado");

        Assert.Contains("Nenhuma campanha encontrada", resposta);
    }

    [Fact]
    public async Task BuscarCampanhas_ComResultados_DeveProjetarCamposCompactos()
    {
        var (tools, handler, _) = CriarTools(_ => Json(
            """
            {
                "items": [{
                    "id": "0d9a04a4-0000-0000-0000-000000000003",
                    "titulo": "Cesta basica para familias",
                    "descricao": "descricao longa que nao deve ir para o modelo...",
                    "dataInicio": "2026-07-01T00:00:00Z",
                    "dataFim": "2026-08-01T00:00:00Z",
                    "metaFinanceira": 10000.0,
                    "valorTotalArrecadado": 2500.0,
                    "status": "Ativa",
                    "categoria": "Alimentacao",
                    "totalDoadores": 12
                }],
                "page": 1, "pageSize": 5, "total": 1, "totalPages": 1
            }
            """));

        var resposta = await tools.BuscarCampanhasAsync("cesta");

        using var json = JsonDocument.Parse(resposta);
        Assert.Equal(1, json.RootElement.GetProperty("totalEncontradas").GetInt32());
        var campanha = json.RootElement.GetProperty("campanhas")[0];
        Assert.Equal("Cesta basica para familias", campanha.GetProperty("titulo").GetString());
        Assert.Equal("Alimentacao", campanha.GetProperty("categoria").GetString());
        // Projecao compacta: a descricao completa NAO vai para o modelo.
        Assert.False(campanha.TryGetProperty("descricao", out _));
        // pageSize fixo em 5 para limitar tokens.
        Assert.Contains("pageSize=5", handler.Requests.Single().RequestUri!.Query);
    }

    [Fact]
    public async Task ObterEstatisticas_SemStats_DeveCairNaVitrineDeTransparencia()
    {
        var (tools, handler, _) = CriarTools(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/stats")
                ? Json("[]")
                : Json("""[{ "titulo": "Agasalho 2026", "metaFinanceira": 5000.0, "valorTotalArrecadado": 1200.0 }]"""));

        var resposta = await tools.ObterEstatisticasAsync();

        using var json = JsonDocument.Parse(resposta);
        Assert.Equal("Agasalho 2026", json.RootElement[0].GetProperty("titulo").GetString());
        Assert.Equal(2, handler.Requests.Count); // stats (vazio) -> fallback transparencia
    }
}
