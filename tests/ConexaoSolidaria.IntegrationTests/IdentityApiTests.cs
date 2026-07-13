using System.Net;
using System.Net.Http.Json;
using ConexaoSolidaria.IntegrationTests.Infrastructure;
using ConexaoSolidaria.Contracts.Auth;
using FluentAssertions;

namespace ConexaoSolidaria.IntegrationTests;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class IdentityApiTests(IntegrationFixture fixture)
{
    [DockerFact]
    public async Task Cadastro_de_doador_valido_retorna_201_com_token()
    {
        var client = fixture.CreateIdentityClient();

        var response = await ApiHelpers.CadastrarDoadorAsync(
            client,
            "Maria Doadora",
            $"maria-{Guid.NewGuid():N}@teste.local",
            CpfGenerator.Next(),
            "SenhaForte#123");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var auth = await response.ReadAsync<AuthDto>();
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.Role.Should().Be(ApplicationRoles.Doador);
        auth.UsuarioId.Should().NotBe(Guid.Empty);
    }

    [DockerFact]
    public async Task Cadastro_com_email_duplicado_retorna_409()
    {
        var client = fixture.CreateIdentityClient();
        var email = $"dup-{Guid.NewGuid():N}@teste.local";

        var first = await ApiHelpers.CadastrarDoadorAsync(client, "Primeiro", email, CpfGenerator.Next(), "SenhaForte#123");
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Mesmo email, CPF diferente -> conflito de email.
        var second = await ApiHelpers.CadastrarDoadorAsync(client, "Segundo", email, CpfGenerator.Next(), "SenhaForte#123");
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [DockerFact]
    public async Task Login_do_gestor_seed_retorna_200_com_role_GestorONG()
    {
        var client = fixture.CreateIdentityClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = IntegrationFixture.GestorEmail,
            senha = IntegrationFixture.GestorSenha
        }, ApiHelpers.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.ReadAsync<AuthDto>();
        auth.Role.Should().Be(ApplicationRoles.GestorOng);
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [DockerFact]
    public async Task Cadastro_com_dados_invalidos_retorna_422()
    {
        var client = fixture.CreateIdentityClient();

        // Email invalido, CPF invalido e senha curta -> 422 (ValidationProblemDetails).
        var response = await ApiHelpers.CadastrarDoadorAsync(
            client,
            "",
            "email-invalido",
            "00000000000",
            "123");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
