using System.Net;
using System.Net.Http.Json;
using ConexaoSolidaria.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.IntegrationTests;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CampaignsApiTests(IntegrationFixture fixture)
{
    [DockerFact]
    public async Task Gestor_cria_campanha_retorna_201()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);

        var campaigns = fixture.CreateCampaignsClient();
        campaigns.UseBearer(gestorToken);

        var now = DateTimeOffset.UtcNow;
        var response = await campaigns.PostAsJsonAsync("/api/campanhas", new
        {
            titulo = "Campanha do Gestor",
            descricao = "Criada por um gestor autorizado.",
            dataInicio = now,
            dataFim = now.AddDays(15),
            metaFinanceira = 5_000m,
            status = "Ativa"
        }, ApiHelpers.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var campanha = await response.ReadAsync<CampanhaDto>();
        campanha.Id.Should().NotBe(Guid.Empty);
        campanha.Status.Should().Be("Ativa");
    }

    [DockerFact]
    public async Task Doador_nao_pode_criar_campanha_retorna_403()
    {
        var identity = fixture.CreateIdentityClient();
        var doador = await ApiHelpers.RegistrarDoadorAsync(identity);

        var campaigns = fixture.CreateCampaignsClient();
        campaigns.UseBearer(doador.Token);

        var now = DateTimeOffset.UtcNow;
        var response = await campaigns.PostAsJsonAsync("/api/campanhas", new
        {
            titulo = "Campanha proibida",
            descricao = "Doador nao pode criar.",
            dataInicio = now,
            dataFim = now.AddDays(10),
            metaFinanceira = 1_000m,
            status = "Ativa"
        }, ApiHelpers.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [DockerFact]
    public async Task Post_doacao_retorna_202_e_grava_outbox_message()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);
        var doador = await ApiHelpers.RegistrarDoadorAsync(identity);

        var campaignsClient = fixture.CreateCampaignsClient();
        var campanha = await ApiHelpers.CriarCampanhaAtivaAsync(campaignsClient, gestorToken);

        var doacaoClient = fixture.CreateCampaignsClient();
        doacaoClient.UseBearer(doador.Token);

        var response = await doacaoClient.PostAsJsonAsync("/api/doacoes", new
        {
            idCampanha = campanha.Id,
            valorDoacao = 150.75m
        }, ApiHelpers.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var aceita = await response.ReadAsync<DoacaoAceitaDto>();
        aceita.DoacaoId.Should().NotBe(Guid.Empty);

        // Verifica no banco (via CampaignsDbContext) que a OutboxMessage foi gravada para esta doacao.
        await using var db = fixture.CreateCampaignsDbContext();
        var outbox = await db.OutboxMessages
            .AsNoTracking()
            .Where(message => message.EventType == "DoacaoRecebidaEvent")
            .ToListAsync();

        outbox.Should().Contain(message => message.Payload.Contains(aceita.DoacaoId.ToString()));
    }

    [DockerFact]
    public async Task Transparencia_lista_campanha_ativa()
    {
        var identity = fixture.CreateIdentityClient();
        var gestorToken = await ApiHelpers.LoginGestorAsync(identity);

        var campaignsClient = fixture.CreateCampaignsClient();
        var titulo = $"Transparencia {Guid.NewGuid():N}";
        await ApiHelpers.CriarCampanhaAtivaAsync(campaignsClient, gestorToken, titulo: titulo);

        // Endpoint anonimo: cliente sem bearer.
        var anon = fixture.CreateCampaignsClient();
        var lista = await anon.GetFromJsonAsync<List<TransparenciaDto>>(
            "/api/campanhas/transparencia", ApiHelpers.Json);

        lista.Should().NotBeNull();
        lista!.Should().Contain(item => item.Titulo == titulo);
    }
}
