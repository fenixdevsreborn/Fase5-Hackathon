using ConexaoSolidaria.Shared.Domain;
using ConexaoSolidaria.Shared.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.Tests;

/// <summary>
/// Garantias de nivel de dados usando Sqlite in-memory (respeita PK/unique de verdade, ao contrario
/// do provider EF InMemory): deduplicacao por EventId e atomicidade do padrao Outbox.
/// </summary>
public sealed class OutboxTests
{
    private static SqliteConnection OpenSharedConnection()
    {
        // Conexao mantida aberta durante o teste => o banco :memory: sobrevive entre contextos.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static CampaignsDbContext NewWorkerContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<CampaignsDbContext>()
            .UseSqlite(connection)
            .Options;
        return new CampaignsDbContext(options);
    }

    private static CampaignsDbContext NewCampaignsContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<CampaignsDbContext>()
            .UseSqlite(connection)
            .Options;
        return new CampaignsDbContext(options);
    }

    [Fact]
    public void ProcessedMessage_DuplicateEventId_ShouldViolatePrimaryKey()
    {
        using var connection = OpenSharedConnection();

        using (var seed = NewWorkerContext(connection))
        {
            seed.Database.EnsureCreated();
        }

        var eventId = Guid.NewGuid();

        // Primeiro consumidor registra o evento como processado.
        using (var first = NewWorkerContext(connection))
        {
            first.ProcessedMessages.Add(new ProcessedMessage
            {
                EventId = eventId,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
            first.SaveChanges();
        }

        // Redelivery concorrente: um segundo consumidor (contexto novo, sem rastreamento do primeiro)
        // tenta gravar o MESMO EventId. A PK de processed_messages deve barrar a duplicata.
        using var second = NewWorkerContext(connection);
        second.ProcessedMessages.Add(new ProcessedMessage
        {
            EventId = eventId,
            ProcessedAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Throws<DbUpdateException>(() => second.SaveChanges());

        // O evento continua registrado uma unica vez.
        using var verify = NewWorkerContext(connection);
        Assert.Equal(1, verify.ProcessedMessages.Count(m => m.EventId == eventId));
    }

    [Fact]
    public void ProcessedMessage_DistinctEventIds_ShouldPersistBoth()
    {
        using var connection = OpenSharedConnection();
        using var context = NewWorkerContext(connection);
        context.Database.EnsureCreated();

        context.ProcessedMessages.Add(new ProcessedMessage { EventId = Guid.NewGuid(), ProcessedAtUtc = DateTimeOffset.UtcNow });
        context.ProcessedMessages.Add(new ProcessedMessage { EventId = Guid.NewGuid(), ProcessedAtUtc = DateTimeOffset.UtcNow });
        context.SaveChanges();

        Assert.Equal(2, context.ProcessedMessages.Count());
    }

    [Fact]
    public void Outbox_DonationAndOutboxMessage_ShouldPersistAtomicallyWithoutIncrementingTotal()
    {
        using var connection = OpenSharedConnection();

        var now = DateTimeOffset.UtcNow;
        var campaign = Campaign.Create(
            "Natal Solidario",
            "Arrecadacao para criancas",
            now.AddDays(-1),
            now.AddDays(10),
            1000m,
            CampaignStatus.Ativa,
            now);

        using (var seed = NewCampaignsContext(connection))
        {
            seed.Database.EnsureCreated();
            seed.Campaigns.Add(campaign);
            seed.SaveChanges();
        }

        var donation = Donation.Create(campaign.Id, Guid.NewGuid(), "doador@exemplo.com", 200m);
        var outbox = OutboxMessage.Create(
            eventType: "DoacaoRecebidaEvent",
            schemaVersion: 1,
            payload: "{\"doacaoId\":\"" + donation.Id + "\"}",
            correlationId: "trace-123");

        // Intencao de doacao: grava doacao + mensagem de outbox num UNICO SaveChanges (atomico).
        using (var write = NewCampaignsContext(connection))
        {
            write.Donations.Add(donation);
            write.OutboxMessages.Add(outbox);
            write.SaveChanges();
        }

        using var verify = NewCampaignsContext(connection);

        var persistedDonation = verify.Donations.Single(d => d.Id == donation.Id);
        var persistedOutbox = verify.OutboxMessages.Single(o => o.Id == outbox.Id);
        var persistedCampaign = verify.Campaigns.Single(c => c.Id == campaign.Id);

        // Ambas as escritas persistiram.
        Assert.Equal(DonationStatus.Pendente, persistedDonation.Status);
        Assert.Equal("DoacaoRecebidaEvent", persistedOutbox.EventType);
        Assert.Null(persistedOutbox.PublishedAtUtc);

        // O total arrecadado NAO e incrementado na intencao (isso ocorre so no worker apos processar).
        Assert.Equal(0m, persistedCampaign.ValorTotalArrecadado);
    }
}
