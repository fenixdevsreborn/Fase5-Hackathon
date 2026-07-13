namespace ConexaoSolidaria.IntegrationTests.Infrastructure;

/// <summary>
/// Variante de <see cref="FactAttribute"/> que marca o teste como Skip (em tempo de descoberta) quando
/// o Docker nao esta disponivel. Garante que a suite compile e execute sempre, ignorando graciosamente
/// os testes de integracao que dependem de containers.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerEnvironment.IsAvailable)
        {
            Skip = DockerEnvironment.SkipReason;
        }
    }
}
