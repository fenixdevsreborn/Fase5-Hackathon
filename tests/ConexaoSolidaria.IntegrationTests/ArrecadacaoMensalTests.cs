using System.Net.Http.Json;
using ConexaoSolidaria.IntegrationTests.Infrastructure;
using ConexaoSolidaria.Shared.Domain;
using FluentAssertions;

namespace ConexaoSolidaria.IntegrationTests;

/// <summary>
/// Serie mensal de arrecadacao (GET /api/campanhas/arrecadacao-mensal). O endpoint monta o
/// agrupamento em SQL cru com AT TIME ZONE, entao o que importa verificar aqui e justamente o que
/// o compilador nao pega: o SQL executa, o DateOnly volta da coluna `date`, o recorte por status e
/// por janela funciona, e a competencia respeita o fuso civil — nao o UTC.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ArrecadacaoMensalTests(IntegrationFixture fixture)
{
    private sealed record PontoMensal(int Ano, int Mes, decimal Total, long Doacoes);

    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    [DockerFact]
    public async Task Serie_soma_apenas_processadas_e_devolve_janela_completa()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);
        var campaigns = fixture.CreateCampaignsClient();
        var campanha = await ApiHelpers.CriarCampanhaAtivaAsync(campaigns, gestorToken);

        // Competencia corrente no fuso de apuracao — a mesma referencia que o endpoint usa.
        var hojeLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Fuso);
        var mesAtual = new DateOnly(hojeLocal.Year, hojeLocal.Month, 1);
        var mesPassado = mesAtual.AddMonths(-1);

        var antes = await ObterSerieAsync(campaigns, meses: 12);

        await SemearAsync(
            campanha.Id,
            // Entram: duas no mes atual, uma no mes passado.
            Processada(150m, LocalParaUtc(mesAtual, dia: 10, hora: 12)),
            Processada(50m, LocalParaUtc(mesAtual, dia: 11, hora: 9)),
            Processada(300m, LocalParaUtc(mesPassado, dia: 5, hora: 15)),
            // Fica de fora: pendente nao arrecadou nada.
            Pendente(9_999m),
            // Fica de fora: processada, mas anterior a janela de 12 meses.
            Processada(7_777m, LocalParaUtc(mesAtual.AddMonths(-20), dia: 3, hora: 12)));

        var depois = await ObterSerieAsync(campaigns, meses: 12);

        depois.Should().HaveCount(12, "a janela sai sempre completa, com os meses vazios zerados");
        depois[^1].Should().Match<PontoMensal>(p => p.Ano == mesAtual.Year && p.Mes == mesAtual.Month);

        // Contigua e crescente: cada ponto e exatamente um mes depois do anterior.
        var competencias = depois.Select(p => new DateOnly(p.Ano, p.Mes, 1)).ToList();
        competencias.Should().Equal(
            Enumerable.Range(0, 12).Select(i => mesAtual.AddMonths(-11 + i)),
            "a serie cobre os 12 meses ate a competencia atual, sem buracos");

        Delta(antes, depois, mesAtual).Total.Should()
            .Be(200m, "150 + 50 processadas no mes corrente; a pendente nao conta");
        Delta(antes, depois, mesAtual).Doacoes.Should().Be(2);

        Delta(antes, depois, mesPassado).Total.Should().Be(300m);

        depois.Sum(p => p.Total).Should().Be(antes.Sum(p => p.Total) + 500m,
            "a doacao de 20 meses atras esta fora da janela e nao entra em nenhum ponto");
    }

    /// <summary>
    /// O caso que justifica o AT TIME ZONE: 31/05 as 22h em Sao Paulo e 01/06 as 01h em UTC.
    /// Agrupar pelo fuso da sessao (UTC) jogaria essa doacao para o mes seguinte.
    /// </summary>
    [DockerFact]
    public async Task Doacao_na_virada_do_mes_cai_na_competencia_local_e_nao_na_utc()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);
        var campaigns = fixture.CreateCampaignsClient();
        var campanha = await ApiHelpers.CriarCampanhaAtivaAsync(campaigns, gestorToken);

        var hojeLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Fuso);
        var mesAtual = new DateOnly(hojeLocal.Year, hojeLocal.Month, 1);
        var mesPassado = mesAtual.AddMonths(-1);

        // Ultimo dia do mes passado, 22h no horario de Sao Paulo => ja e dia 1 em UTC.
        var ultimoDia = DateTime.DaysInMonth(mesPassado.Year, mesPassado.Month);
        var viradaUtc = LocalParaUtc(mesPassado, ultimoDia, hora: 22);

        viradaUtc.UtcDateTime.Month.Should().Be(mesAtual.Month,
            "o teste so prova algo se o instante realmente cair no mes seguinte em UTC");

        var antes = await ObterSerieAsync(campaigns, meses: 12);
        await SemearAsync(campanha.Id, Processada(400m, viradaUtc));
        var depois = await ObterSerieAsync(campaigns, meses: 12);

        Delta(antes, depois, mesPassado).Total.Should().Be(400m,
            "a competencia e a civil brasileira: 31/05 as 22h pertence a maio");
        Delta(antes, depois, mesAtual).Total.Should().Be(0m,
            "agrupar em UTC jogaria a doacao para o mes seguinte");
    }

    [DockerFact]
    public async Task Janela_respeita_o_parametro_meses()
    {
        var campaigns = fixture.CreateCampaignsClient();

        (await ObterSerieAsync(campaigns, meses: 3)).Should().HaveCount(3);
        // Fora da faixa aceita (1..36) o servidor satura em vez de recusar.
        (await ObterSerieAsync(campaigns, meses: 999)).Should().HaveCount(36);
        (await ObterSerieAsync(campaigns, meses: 0)).Should().HaveCount(12, "0 cai no default");
    }

    // ----- apoio -----

    private static async Task<IReadOnlyList<PontoMensal>> ObterSerieAsync(HttpClient client, int meses)
    {
        var response = await client.GetAsync($"/api/campanhas/arrecadacao-mensal?meses={meses}");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<List<PontoMensal>>();
    }

    private static PontoMensal Ponto(IReadOnlyList<PontoMensal> serie, DateOnly competencia) =>
        serie.Single(p => p.Ano == competencia.Year && p.Mes == competencia.Month);

    /// <summary>
    /// Variacao de um mes entre duas leituras. A suite compartilha um banco por colecao, entao o
    /// endpoint (que agrega TODAS as doacoes, sem filtro por campanha) enxerga tambem o que os
    /// outros testes gravaram. Medir o delta torna a assercao independente da ordem de execucao.
    /// </summary>
    private static PontoMensal Delta(
        IReadOnlyList<PontoMensal> antes,
        IReadOnlyList<PontoMensal> depois,
        DateOnly competencia)
    {
        var a = Ponto(antes, competencia);
        var d = Ponto(depois, competencia);
        return new PontoMensal(competencia.Year, competencia.Month, d.Total - a.Total, d.Doacoes - a.Doacoes);
    }

    /// <summary>Instante local (no fuso de apuracao) convertido para o UTC que vai ao banco.</summary>
    private static DateTimeOffset LocalParaUtc(DateOnly competencia, int dia, int hora)
    {
        var local = new DateTime(competencia.Year, competencia.Month, dia, hora, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Fuso), TimeSpan.Zero);
    }

    private static Donation Processada(decimal valor, DateTimeOffset processadaEm)
    {
        var doacao = Donation.Create(Guid.NewGuid(), Guid.NewGuid(), "doador@teste.local", valor);
        doacao.MarkAsProcessed(processadaEm);
        return doacao;
    }

    private static Donation Pendente(decimal valor) =>
        Donation.Create(Guid.NewGuid(), Guid.NewGuid(), "doador@teste.local", valor);

    // Grava direto no banco: o caminho normal (POST + worker) nao permite escolher a data de
    // processamento, que e exatamente a variavel sob teste aqui.
    private async Task SemearAsync(Guid campaignId, params Donation[] doacoes)
    {
        await using var db = fixture.CreateCampaignsDbContext();

        foreach (var doacao in doacoes)
        {
            db.Entry(doacao).Property(nameof(Donation.CampaignId)).CurrentValue = campaignId;
            db.Donations.Add(doacao);
        }

        await db.SaveChangesAsync();
    }
}
