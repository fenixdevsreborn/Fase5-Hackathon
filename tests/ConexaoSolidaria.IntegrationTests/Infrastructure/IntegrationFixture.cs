using System.Text;
using System.Text.Json;
using ConexaoSolidaria.Campaigns.Api.Controllers;
using ConexaoSolidaria.Campaigns.Api.Repositories;
using ConexaoSolidaria.Identity.Api.Controllers;
using ConexaoSolidaria.Donations.Worker.Messaging;
using ConexaoSolidaria.Contracts.Events;
using ConexaoSolidaria.Contracts.Messaging;
using ConexaoSolidaria.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace ConexaoSolidaria.IntegrationTests.Infrastructure;

/// <summary>
/// Harness compartilhado por toda a collection de integracao. Sobe UM PostgreSQL e UM RabbitMQ via
/// Testcontainers, cria os dois bancos distintos (identitydb + campaignsdb) no mesmo Postgres, e
/// hospeda:
/// <list type="bullet">
/// <item>Identity.Api e Campaigns.Api via <see cref="TestApiFactory{TEntryPoint}"/> (as migrations
/// rodam no startup de cada API);</item>
/// <item>o <c>DonationConsumerWorker</c> num <see cref="IHost"/> dedicado apontando aos mesmos
/// containers (o <c>OutboxDispatcherWorker</c> ja roda dentro do host da Campaigns.Api).</item>
/// </list>
/// Se o Docker nao estiver disponivel, <see cref="InitializeAsync"/> retorna sem subir nada (os
/// testes sao skipados por <see cref="DockerFactAttribute"/>), preservando a robustez da suite.
/// </summary>
public sealed class IntegrationFixture : IAsyncLifetime
{
    public const string JwtSecret = "conexao-solidaria-integration-tests-super-secret-key-0123456789-abcdef";
    public const string JwtIssuer = "ConexaoSolidaria";
    public const string JwtAudience = "ConexaoSolidaria";
    public const string GestorEmail = "gestor@conexaosolidaria.local";
    public const string GestorSenha = "SenhaGestor#Teste123";

    private const string IdentityDbName = "identitydb";
    private const string CampaignsDbName = "campaignsdb";

    private PostgreSqlContainer? _postgres;
    private RabbitMqContainer? _rabbit;

    private TestApiFactory<AuthController>? _identityFactory;
    private TestApiFactory<CampanhasController>? _campaignsFactory;
    private IHost? _workerHost;

    public bool Started { get; private set; }

    public string CampaignsConnectionString { get; private set; } = string.Empty;

    public string IdentityConnectionString { get; private set; } = string.Empty;

    public string RabbitConnectionString { get; private set; } = string.Empty;

    public RabbitMqOptions RabbitOptions { get; } = new();

    public async Task InitializeAsync()
    {
        if (!DockerEnvironment.IsAvailable)
        {
            // Sem Docker: nao ha o que subir. Os testes serao skipados pelo DockerFactAttribute.
            return;
        }

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management-alpine")
            .Build();

        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        var basePostgres = _postgres.GetConnectionString();
        IdentityConnectionString = WithDatabase(basePostgres, IdentityDbName);
        CampaignsConnectionString = WithDatabase(basePostgres, CampaignsDbName);
        RabbitConnectionString = _rabbit.GetConnectionString();

        await CreateDatabasesAsync(basePostgres, IdentityDbName, CampaignsDbName);

        // As APIs (minimal hosting) leem Jwt:Secret e as connection strings de builder.Configuration
        // ANTES de builder.Build(); nesse ponto o ConfigureAppConfiguration do WebApplicationFactory
        // ainda nao foi aplicado. Variaveis de ambiente sao lidas por WebApplication.CreateBuilder no
        // momento da construcao, entao injetamos por elas (as chaves nao colidem entre as duas APIs).
        SetProcessEnvironment(new Dictionary<string, string?>
        {
            ["Jwt__Secret"] = JwtSecret,
            ["Jwt__Issuer"] = JwtIssuer,
            ["Jwt__Audience"] = JwtAudience,
            ["ConnectionStrings__IdentityDb"] = IdentityConnectionString,
            ["ConnectionStrings__CampaignsDb"] = CampaignsConnectionString,
            ["ConnectionStrings__messaging"] = RabbitConnectionString,
            ["Seed__Gestor__Email"] = GestorEmail,
            ["Seed__Gestor__Senha"] = GestorSenha
        });

        _identityFactory = new TestApiFactory<AuthController>(BuildIdentitySettings());
        _campaignsFactory = new TestApiFactory<CampanhasController>(
            BuildCampaignsSettings(),
            services =>
            {
                // Remove o Elasticsearch real: nos testes a indexacao e no-op.
                services.RemoveAll<ICampaignSearchRepository>();
                services.AddScoped<ICampaignSearchRepository, FakeCampaignSearchRepository>();
            });

        // Forca o startup dos hosts das APIs (dispara MigrateAsync + hosted services como o
        // OutboxDispatcherWorker). A ordem garante que o schema do campaignsdb exista antes do worker.
        _ = _campaignsFactory.CreateClient();
        _ = _identityFactory.CreateClient();

        await StartWorkerHostAsync();

        Started = true;
    }

    public async Task DisposeAsync()
    {
        if (_workerHost is not null)
        {
            try
            {
                await _workerHost.StopAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // ignore
            }

            _workerHost.Dispose();
        }

        if (_identityFactory is not null)
        {
            await _identityFactory.DisposeAsync();
        }

        if (_campaignsFactory is not null)
        {
            await _campaignsFactory.DisposeAsync();
        }

        if (_rabbit is not null)
        {
            await _rabbit.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    public HttpClient CreateIdentityClient() => _identityFactory!.CreateClient();

    public HttpClient CreateCampaignsClient() => _campaignsFactory!.CreateClient();

    /// <summary>DbContext direto sobre o campaignsdb para assercoes de estado (outbox, totais, dedup).</summary>
    public CampaignsDbContext CreateCampaignsDbContext()
    {
        var options = new DbContextOptionsBuilder<CampaignsDbContext>()
            .UseNpgsql(CampaignsConnectionString)
            .Options;

        return new CampaignsDbContext(options);
    }

    /// <summary>
    /// Publica um <see cref="DoacaoRecebidaEvent"/> cru diretamente na exchange principal (bypass do
    /// Outbox), usado para exercitar o consumo/idempotencia do worker com um EventId controlado.
    /// </summary>
    public async Task PublishDonationEventAsync(DoacaoRecebidaEvent donationEvent)
    {
        var factory = new ConnectionFactory { Uri = new Uri(RabbitConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Declaracao idempotente da exchange principal (mesmos parametros do publisher/worker).
        await channel.ExchangeDeclareAsync(
            exchange: RabbitOptions.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        var body = JsonSerializer.SerializeToUtf8Bytes(donationEvent);
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = donationEvent.EventoId.ToString(),
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: RabbitOptions.ExchangeName,
            routingKey: RabbitOptions.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    private async Task StartWorkerHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CampaignsDb"] = CampaignsConnectionString,
            ["ConnectionStrings:messaging"] = RabbitConnectionString
        });

        builder.Services.Configure<RabbitMqOptions>(
            builder.Configuration.GetSection(RabbitMqOptions.SectionName));
        builder.Services.AddDbContext<CampaignsDbContext>(options =>
            options.UseNpgsql(CampaignsConnectionString));
        builder.Services.AddHostedService<DonationConsumerWorker>();

        _workerHost = builder.Build();
        await _workerHost.StartAsync();
    }

    private Dictionary<string, string?> BuildIdentitySettings() => new()
    {
        ["ConnectionStrings:IdentityDb"] = IdentityConnectionString,
        ["Jwt:Secret"] = JwtSecret,
        ["Jwt:Issuer"] = JwtIssuer,
        ["Jwt:Audience"] = JwtAudience,
        ["Seed:Gestor:Email"] = GestorEmail,
        ["Seed:Gestor:Senha"] = GestorSenha
    };

    private Dictionary<string, string?> BuildCampaignsSettings() => new()
    {
        ["ConnectionStrings:CampaignsDb"] = CampaignsConnectionString,
        ["ConnectionStrings:messaging"] = RabbitConnectionString,
        ["Jwt:Secret"] = JwtSecret,
        ["Jwt:Issuer"] = JwtIssuer,
        ["Jwt:Audience"] = JwtAudience
    };

    private static void SetProcessEnvironment(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var (key, value) in values)
        {
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
        }
    }

    private static string WithDatabase(string baseConnectionString, string database)
    {
        return new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = database
        }.ConnectionString;
    }

    private static async Task CreateDatabasesAsync(string maintenanceConnectionString, params string[] databases)
    {
        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync();

        foreach (var database in databases)
        {
            await using var command = connection.CreateCommand();
            // Idempotente: cria apenas se ainda nao existir.
            command.CommandText =
                $"SELECT 1 FROM pg_database WHERE datname = '{database}'";
            var exists = await command.ExecuteScalarAsync() is not null;

            if (!exists)
            {
                await using var create = connection.CreateCommand();
                create.CommandText = $"CREATE DATABASE {database}";
                await create.ExecuteNonQueryAsync();
            }
        }
    }
}
