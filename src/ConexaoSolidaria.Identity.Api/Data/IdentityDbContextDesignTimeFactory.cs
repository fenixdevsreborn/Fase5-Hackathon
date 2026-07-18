using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConexaoSolidaria.Identity.Api.Data;

/// <summary>
/// Fabrica usada APENAS em design-time pelo `dotnet ef` (ex.: `migrations add`, `dbcontext info`).
/// A connection string abaixo e um placeholder para o scaffolding das migrations e NUNCA e usada
/// em runtime (o Program.cs resolve a conexao real via configuracao). Nao e segredo.
/// </summary>
public sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=identitydb;Username=postgres;Password=postgres")
            .Options;

        return new IdentityDbContext(options);
    }
}
