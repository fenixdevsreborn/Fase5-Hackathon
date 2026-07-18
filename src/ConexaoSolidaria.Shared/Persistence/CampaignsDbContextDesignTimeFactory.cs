using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConexaoSolidaria.Shared.Persistence;

/// <summary>
/// Fabrica usada APENAS em design-time pelo `dotnet ef` (ex.: `migrations add`, `dbcontext info`).
/// A connection string abaixo e um placeholder para o scaffolding das migrations e NUNCA e usada
/// em runtime (o Program.cs resolve a conexao real via configuracao). Nao e segredo.
/// </summary>
public sealed class CampaignsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CampaignsDbContext>
{
    public CampaignsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CampaignsDbContext>()
            .UseNpgsql("Host=localhost;Database=campaignsdb;Username=postgres;Password=postgres")
            .Options;

        return new CampaignsDbContext(options);
    }
}
