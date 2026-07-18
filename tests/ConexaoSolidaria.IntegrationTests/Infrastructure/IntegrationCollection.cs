namespace ConexaoSolidaria.IntegrationTests.Infrastructure;

/// <summary>
/// Collection unica: os containers/hosts (caros de subir) sao compartilhados por todos os testes de
/// integracao via <see cref="IntegrationFixture"/>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationFixture>
{
    public const string Name = "Integration";
}
