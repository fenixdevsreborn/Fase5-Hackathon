using System.Net;
using System.Net.Http.Json;
using ConexaoSolidaria.IntegrationTests.Infrastructure;
using ConexaoSolidaria.Shared.Domain;
using ConexaoSolidaria.Contracts.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.IntegrationTests;

/// <summary>
/// Testes do fluxo assincrono ponta a ponta com Postgres + RabbitMQ reais:
/// Outbox (Campaigns.Api) -> fila -> DonationConsumerWorker -> total da campanha atualizado.
/// Inclui idempotencia por EventId (dedup no worker) e por header Idempotency-Key (na API).
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AsyncFlowTests(IntegrationFixture fixture)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(500);

    [DockerFact]
    public async Task Fluxo_completo_processa_doacao_e_atualiza_total()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);
        var doador = await ApiHelpers.RegistrarDoadorAsync(identity);

        var campaignsClient = fixture.CreateCampaignsClient();
        var campanha = await ApiHelpers.CriarCampanhaAtivaAsync(campaignsClient, gestorToken);

        const decimal valor = 200.50m;
        var doacaoClient = fixture.CreateCampaignsClient();
        doacaoClient.UseBearer(doador.Token);

        var post = await doacaoClient.PostAsJsonAsync("/api/doacoes", new
        {
            idCampanha = campanha.Id,
            valorDoacao = valor
        }, ApiHelpers.Json);

        post.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var aceita = await post.ReadAsync<DoacaoAceitaDto>();

        // Polling: o worker (via Outbox -> fila) deve concluir o processamento em ate ~40s.
        var processada = await WaitUntilAsync(async () =>
        {
            var status = await doacaoClient.GetFromJsonAsync<DoacaoStatusDto>(
                $"/api/doacoes/{aceita.DoacaoId}", ApiHelpers.Json);
            return status is not null && status.Status == nameof(DonationStatus.Processada);
        });

        processada.Should().BeTrue("a doacao deve ser processada assincronamente pelo worker");

        // O total arrecadado da campanha subiu exatamente o valor da doacao.
        await using var db = fixture.CreateCampaignsDbContext();
        var totalAtual = await db.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == campanha.Id)
            .Select(c => c.ValorTotalArrecadado)
            .SingleAsync();

        totalAtual.Should().Be(valor);
    }

    [DockerFact]
    public async Task Evento_repetido_com_mesmo_EventId_nao_dobra_total_e_deduplica()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);

        var campaignsClient = fixture.CreateCampaignsClient();
        var campanha = await ApiHelpers.CriarCampanhaAtivaAsync(campaignsClient, gestorToken);

        // Cria uma doacao Pendente diretamente no banco (controle total sobre o EventId publicado).
        const decimal valor = 321.00m;
        var doadorId = Guid.NewGuid();
        var donation = Donation.Create(campanha.Id, doadorId, "dedup@teste.local", valor);

        await using (var seed = fixture.CreateCampaignsDbContext())
        {
            seed.Donations.Add(donation);
            await seed.SaveChangesAsync();
        }

        var eventId = Guid.NewGuid();
        var evento = new DoacaoRecebidaEvent(
            eventId,
            donation.Id,
            campanha.Id,
            doadorId,
            "dedup@teste.local",
            valor,
            DateTimeOffset.UtcNow,
            CorrelationId: $"dedup-{eventId:N}");

        // Publica o MESMO evento duas vezes na fila principal.
        await fixture.PublishDonationEventAsync(evento);
        await fixture.PublishDonationEventAsync(evento);

        // Aguarda o processamento (doacao vira Processada).
        var processada = await WaitUntilAsync(async () =>
        {
            await using var db = fixture.CreateCampaignsDbContext();
            var status = await db.Donations
                .AsNoTracking()
                .Where(d => d.Id == donation.Id)
                .Select(d => d.Status)
                .SingleAsync();
            return status == DonationStatus.Processada;
        });

        processada.Should().BeTrue("o worker deve processar a doacao publicada diretamente na fila");

        // Da uma folga para a segunda entrega (duplicada) ser consumida e deduplicada.
        await Task.Delay(TimeSpan.FromSeconds(2));

        await using var assertDb = fixture.CreateCampaignsDbContext();

        var total = await assertDb.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == campanha.Id)
            .Select(c => c.ValorTotalArrecadado)
            .SingleAsync();

        // Total incrementado UMA vez (nao dobrado), apesar das duas publicacoes.
        total.Should().Be(valor);

        var processedCount = await assertDb.ProcessedMessages
            .AsNoTracking()
            .CountAsync(message => message.EventId == eventId);

        processedCount.Should().Be(1);
    }

    [DockerFact]
    public async Task Duas_doacoes_com_mesma_Idempotency_Key_retornam_a_mesma_doacao()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);
        var doador = await ApiHelpers.RegistrarDoadorAsync(identity);

        var campaignsClient = fixture.CreateCampaignsClient();
        var campanha = await ApiHelpers.CriarCampanhaAtivaAsync(campaignsClient, gestorToken);

        var doacaoClient = fixture.CreateCampaignsClient();
        doacaoClient.UseBearer(doador.Token);

        var idempotencyKey = $"key-{Guid.NewGuid():N}";
        doacaoClient.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var body = new { idCampanha = campanha.Id, valorDoacao = 99.90m };

        var first = await doacaoClient.PostAsJsonAsync("/api/doacoes", body, ApiHelpers.Json);
        var second = await doacaoClient.PostAsJsonAsync("/api/doacoes", body, ApiHelpers.Json);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var firstDto = await first.ReadAsync<DoacaoAceitaDto>();
        var secondDto = await second.ReadAsync<DoacaoAceitaDto>();

        // Mesma chave -> mesma doacao (nao cria duplicata).
        secondDto.DoacaoId.Should().Be(firstDto.DoacaoId);

        await using var db = fixture.CreateCampaignsDbContext();
        var doacoesDaCampanha = await db.Donations
            .AsNoTracking()
            .CountAsync(d => d.CampaignId == campanha.Id);

        doacoesDaCampanha.Should().Be(1);
    }

    [DockerFact]
    public async Task Read_model_campaign_stats_e_populado_e_acumulado_por_doacao_processada()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);

        var campaignsClient = fixture.CreateCampaignsClient();
        var campanha = await ApiHelpers.CriarCampanhaAtivaAsync(campaignsClient, gestorToken);

        // Duas doacoes distintas para a MESMA campanha: a primeira exercita o INSERT do read model,
        // a segunda o caminho ON CONFLICT DO UPDATE (acumulo de total e contagem).
        const decimal valor1 = 150.00m;
        const decimal valor2 = 275.50m;
        var doadorId = Guid.NewGuid();

        var donation1 = Donation.Create(campanha.Id, doadorId, "stats1@teste.local", valor1);
        var donation2 = Donation.Create(campanha.Id, doadorId, "stats2@teste.local", valor2);

        await using (var seed = fixture.CreateCampaignsDbContext())
        {
            seed.Donations.Add(donation1);
            seed.Donations.Add(donation2);
            await seed.SaveChangesAsync();
        }

        await fixture.PublishDonationEventAsync(NovoEvento(donation1.Id, campanha.Id, doadorId, "stats1@teste.local", valor1));
        await fixture.PublishDonationEventAsync(NovoEvento(donation2.Id, campanha.Id, doadorId, "stats2@teste.local", valor2));

        // Aguarda as duas doacoes serem processadas.
        var processadas = await WaitUntilAsync(async () =>
        {
            await using var db = fixture.CreateCampaignsDbContext();
            var count = await db.Donations
                .AsNoTracking()
                .CountAsync(d => d.CampaignId == campanha.Id && d.Status == DonationStatus.Processada);
            return count == 2;
        });

        processadas.Should().BeTrue("o worker deve processar as duas doacoes publicadas na fila");

        // O read model reflete o total acumulado e a contagem, com Titulo/Meta da campanha.
        await using var assertDb = fixture.CreateCampaignsDbContext();
        var stats = await assertDb.CampaignStats
            .AsNoTracking()
            .SingleAsync(s => s.CampaignId == campanha.Id);

        stats.TotalArrecadado.Should().Be(valor1 + valor2);
        stats.DoacoesProcessadas.Should().Be(2);
        stats.MetaFinanceira.Should().Be(campanha.MetaFinanceira);
        stats.Titulo.Should().NotBeNullOrWhiteSpace();
    }

    private static DoacaoRecebidaEvent NovoEvento(
        Guid doacaoId,
        Guid campanhaId,
        Guid doadorId,
        string email,
        decimal valor)
    {
        var eventId = Guid.NewGuid();
        return new DoacaoRecebidaEvent(
            eventId,
            doacaoId,
            campanhaId,
            doadorId,
            email,
            valor,
            DateTimeOffset.UtcNow,
            CorrelationId: $"stats-{eventId:N}");
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var cts = new CancellationTokenSource(Timeout);
        while (!cts.IsCancellationRequested)
        {
            try
            {
                if (await condition())
                {
                    return true;
                }
            }
            catch
            {
                // Estado ainda nao disponivel (ex.: doacao nao encontrada); tenta de novo.
            }

            try
            {
                await Task.Delay(Poll, cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        return false;
    }
}
