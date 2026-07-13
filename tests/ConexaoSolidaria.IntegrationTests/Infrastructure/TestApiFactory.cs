using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConexaoSolidaria.IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> parametrizado por um tipo-marcador de cada API
/// (um controller). Usar um marcador especifico de cada assembly em vez de <c>Program</c> evita a
/// ambiguidade CS0433 (as duas APIs expoem <c>public partial class Program</c> no namespace global).
/// Injeta connection strings/segredos dos containers via configuracao in-memory (sobrepondo o
/// appsettings) e permite trocar servicos (ex.: Elasticsearch) via <see cref="ConfigureServices"/>.
/// </summary>
public sealed class TestApiFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly IReadOnlyDictionary<string, string?> _settings;
    private readonly Action<IServiceCollection>? _configureServices;

    public TestApiFactory(
        IReadOnlyDictionary<string, string?> settings,
        Action<IServiceCollection>? configureServices = null)
    {
        _settings = settings;
        _configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(_settings);
        });

        if (_configureServices is not null)
        {
            builder.ConfigureTestServices(_configureServices);
        }
    }
}
