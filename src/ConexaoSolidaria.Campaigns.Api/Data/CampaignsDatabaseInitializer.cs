using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.Campaigns.Api.Data;

public static class CampaignsDatabaseInitializer
{
    private const string CampaignBusinessKeyIndexSql =
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_campaigns_titulo_data_inicio_data_fim_status " +
        "ON campaigns (\"Titulo\", \"DataInicio\", \"DataFim\", \"Status\");";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CampaignsDatabaseInitializer");

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
                logger.LogWarning(ex, "Banco de campanhas indisponivel. Tentativa {Attempt}/10.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
