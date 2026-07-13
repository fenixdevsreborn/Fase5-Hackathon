using ConexaoSolidaria.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.Donations.Worker.Data;

public static class WorkerDatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("WorkerDatabaseInitializer");

        // O Worker NAO migra: quem e dono do schema do 'campaignsdb' e a Campaigns.Api. Aqui apenas
        // aguardamos o banco responder E as tabelas existirem (migrations ja aplicadas pela API),
        // consultando uma tabela real dentro de um try/catch. Enquanto o schema nao estiver pronto
        // a consulta lanca e retentamos.
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                if (await db.Database.CanConnectAsync())
                {
                    // Se as tabelas ja existem, esta consulta funciona; se ainda nao migraram, lanca e retenta.
                    await db.Donations.AnyAsync();
                    logger.LogInformation("Schema do banco de campanhas pronto para o worker.");
                    return;
                }

                logger.LogWarning("Banco de campanhas ainda nao aceita conexao para o worker. Tentativa {Attempt}/10.", attempt);
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(ex, "Schema do banco de campanhas ainda nao pronto para o worker. Tentativa {Attempt}/10.", attempt);
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
