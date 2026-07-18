using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ConexaoSolidaria.IntegrationTests.Infrastructure;

// DTOs de leitura das respostas (JSON camelCase das APIs).
public sealed record AuthDto(
    Guid UsuarioId,
    string NomeCompleto,
    string Email,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiraEm);

public sealed record CampanhaDto(
    Guid Id,
    string Titulo,
    string Descricao,
    DateTimeOffset DataInicio,
    DateTimeOffset DataFim,
    decimal MetaFinanceira,
    decimal ValorTotalArrecadado,
    string Status);

public sealed record DoacaoAceitaDto(
    Guid DoacaoId,
    Guid CampanhaId,
    decimal ValorDoacao,
    string Status,
    string Mensagem);

public sealed record DoacaoStatusDto(
    Guid DoacaoId,
    Guid CampanhaId,
    decimal ValorDoacao,
    string Status);

public sealed record TransparenciaDto(
    string Titulo,
    decimal MetaFinanceira,
    decimal ValorTotalArrecadado);

/// <summary>Novo doador criado no fluxo de teste (credenciais + token).</summary>
public sealed record DoadorCriado(AuthDto Auth, string Email, string Cpf, string Senha, string Token);

/// <summary>Helpers de alto nivel para exercitar as APIs nos testes.</summary>
public static class ApiHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void UseBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(Json);
        return payload ?? throw new InvalidOperationException(
            $"Nao foi possivel desserializar a resposta em {typeof(T).Name}.");
    }

    public static async Task<HttpResponseMessage> CadastrarDoadorAsync(
        HttpClient identityClient,
        string nome,
        string email,
        string cpf,
        string senha)
    {
        return await identityClient.PostAsJsonAsync("/api/auth/cadastro-doador", new
        {
            nomeCompleto = nome,
            email,
            cpf,
            senha
        }, Json);
    }

    public static async Task<DoadorCriado> RegistrarDoadorAsync(HttpClient identityClient)
    {
        var email = $"doador-{Guid.NewGuid():N}@teste.local";
        var cpf = CpfGenerator.Next();
        const string senha = "SenhaDoador#123";

        var response = await CadastrarDoadorAsync(identityClient, "Doador Teste", email, cpf, senha);
        response.EnsureSuccessStatusCode();

        var auth = await response.ReadAsync<AuthDto>();
        return new DoadorCriado(auth, email, cpf, senha, auth.AccessToken);
    }

    public static async Task<string> LoginGestorAsync(HttpClient identityClient)
    {
        var response = await identityClient.PostAsJsonAsync("/api/auth/login", new
        {
            email = IntegrationFixture.GestorEmail,
            senha = IntegrationFixture.GestorSenha
        }, Json);

        response.EnsureSuccessStatusCode();
        var auth = await response.ReadAsync<AuthDto>();
        return auth.AccessToken;
    }

    public static async Task<CampanhaDto> CriarCampanhaAtivaAsync(
        HttpClient campaignsClient,
        string gestorToken,
        decimal meta = 10_000m,
        string? titulo = null)
    {
        campaignsClient.UseBearer(gestorToken);

        var now = DateTimeOffset.UtcNow;
        var response = await campaignsClient.PostAsJsonAsync("/api/campanhas", new
        {
            titulo = titulo ?? $"Campanha {Guid.NewGuid():N}",
            descricao = "Campanha de teste de integracao.",
            dataInicio = now.AddDays(-1),
            dataFim = now.AddDays(30),
            metaFinanceira = meta,
            status = "Ativa"
        }, Json);

        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<CampanhaDto>();
    }
}
