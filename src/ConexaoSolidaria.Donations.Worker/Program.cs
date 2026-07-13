using ConexaoSolidaria.Donations.Worker.Data;
using ConexaoSolidaria.Donations.Worker.Messaging;
using ConexaoSolidaria.Contracts.Messaging;
using ConexaoSolidaria.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
// EnableRetryOnFailure: torna o EF resiliente a falhas transitorias de conexao/comando (retry
// automatico com backoff). ATENCAO: isso cria uma execution strategy que NAO permite abrir
// transacao manual (BeginTransactionAsync) diretamente -> o bloco transacional do worker e
// envolvido em strategy.ExecuteAsync (ver ProcessDonationAsync no DonationConsumerWorker).
builder.Services.AddDbContext<CampaignsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("CampaignsDb"),
        npgsql => npgsql.EnableRetryOnFailure()));
builder.Services.AddHostedService<DonationConsumerWorker>();
builder.Services.AddHealthChecks();

var app = builder.Build();

await WorkerDatabaseInitializer.InitializeAsync(app.Services);

// /health e /alive vem do ServiceDefaults (MapDefaultEndpoints); /metrics segue no prometheus-net.
app.MapDefaultEndpoints();
app.MapMetrics();

app.Run();
