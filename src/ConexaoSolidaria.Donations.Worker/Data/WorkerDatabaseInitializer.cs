using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.Donations.Worker.Data;

public static class WorkerDatabaseInitializer
{
    private const string CampaignBusinessKeyIndexSql =
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_campaigns_titulo_data_inicio_data_fim_status " +
        "ON campaigns (\"Titulo\", \"DataInicio\", \"DataFim\", \"Status\");";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerCampaignsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("WorkerDatabaseInitializer");

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync();
                await db.Database.ExecuteSqlRawAsync(CampaignBusinessKeyIndexSql);
                return;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(ex, "Banco de campanhas indisponivel para o worker. Tentativa {Attempt}/10.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
