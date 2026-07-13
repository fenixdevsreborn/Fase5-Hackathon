using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ConexaoSolidaria.Web.Services;

/// <summary>
/// Cliente HTTP tipado que fala com o Gateway (base address "http://gateway",
/// resolvido via service discovery do Aspire). Anexa o header
/// Authorization: Bearer {token} quando ha usuario logado.
///
/// Convencao de erro: em falha HTTP (status != sucesso) os metodos retornam null
/// (ou uma pagina vazia) para que as paginas exibam estado de erro/vazio.
/// </summary>
public sealed class ApiClient(HttpClient http, TokenProvider tokenProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ---------- Autenticacao ----------

    public Task<AuthResult?> LoginAsync(string email, string senha, CancellationToken ct = default) =>
        PostAsync<AuthResult>("api/auth/login", new { email, senha }, ct);

    public Task<AuthResult?> CadastrarDoadorAsync(CadastroDoador dto, CancellationToken ct = default) =>
        PostAsync<AuthResult>("api/auth/cadastro-doador", dto, ct);

    // ---------- Campanhas (publico + gestor) ----------

    public async Task<Paginated<CampanhaDto>> BuscarCampanhasAsync(
        string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var url = $"api/campanhas/search?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(q))
        {
            url += $"&q={Uri.EscapeDataString(q)}";
        }

        var result = await GetAsync<Paginated<CampanhaDto>>(url, ct);
        return result ?? Paginated<CampanhaDto>.Empty(page, pageSize);
    }

    public Task<CampanhaDto?> ObterCampanhaAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<CampanhaDto>($"api/campanhas/{id}", ct);

    public async Task<IReadOnlyList<TransparenciaDto>> TransparenciaAsync(CancellationToken ct = default) =>
        await GetAsync<IReadOnlyList<TransparenciaDto>>("api/campanhas/transparencia", ct)
        ?? Array.Empty<TransparenciaDto>();

    public Task<CampanhaDto?> CriarCampanhaAsync(SalvarCampanha dto, CancellationToken ct = default) =>
        PostAsync<CampanhaDto>("api/campanhas", dto, ct);

    public Task<CampanhaDto?> AtualizarCampanhaAsync(Guid id, SalvarCampanha dto, CancellationToken ct = default) =>
        PutAsync<CampanhaDto>($"api/campanhas/{id}", dto, ct);

    // ---------- Doacoes (doador) ----------

    public Task<DoacaoAceitaDto?> DoarAsync(Guid idCampanha, decimal valor, CancellationToken ct = default) =>
        PostAsync<DoacaoAceitaDto>("api/doacoes", new { idCampanha, valorDoacao = valor }, ct);

    public Task<DoacaoStatusDto?> StatusDoacaoAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<DoacaoStatusDto>($"api/doacoes/{id}", ct);

    public async Task<IReadOnlyList<MinhaDoacaoDto>> MinhasDoacoesAsync(CancellationToken ct = default) =>
        await GetAsync<IReadOnlyList<MinhaDoacaoDto>>("api/doacoes/minhas", ct)
        ?? Array.Empty<MinhaDoacaoDto>();

    // ---------- Ciclo de vida de campanhas (gestor) ----------

    public Task<CampanhaDto?> AtivarCampanhaAsync(Guid id, CancellationToken ct = default) =>
        PostAsync<CampanhaDto>($"api/campanhas/{id}/ativar", new { }, ct);

    public Task<CampanhaDto?> ConcluirCampanhaAsync(Guid id, CancellationToken ct = default) =>
        PostAsync<CampanhaDto>($"api/campanhas/{id}/concluir", new { }, ct);

    public Task<CampanhaDto?> CancelarCampanhaAsync(Guid id, CancellationToken ct = default) =>
        PostAsync<CampanhaDto>($"api/campanhas/{id}/cancelar", new { }, ct);

    // ---------- Infra HTTP ----------

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            using var response = await http.SendAsync(request, ct);
            return await ReadAsync<T>(response, ct);
        }
        catch (HttpRequestException)
        {
            return default;
        }
    }

    private async Task<T?> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, url);
            request.Content = JsonContent.Create(body, options: JsonOptions);
            using var response = await http.SendAsync(request, ct);
            return await ReadAsync<T>(response, ct);
        }
        catch (HttpRequestException)
        {
            return default;
        }
    }

    private async Task<T?> PutAsync<T>(string url, object body, CancellationToken ct)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Put, url);
            request.Content = JsonContent.Create(body, options: JsonOptions);
            using var response = await http.SendAsync(request, ct);
            return await ReadAsync<T>(response, ct);
        }
        catch (HttpRequestException)
        {
            return default;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        if (tokenProvider.HasToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenProvider.Token);
        }

        return request;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
        {
            return default;
        }

        if (response.Content.Headers.ContentLength is 0)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }
}
