namespace ConexaoSolidaria.Web.Services;

/// <summary>
/// Guarda o JWT do usuario logado em memoria, no escopo do circuito Blazor.
/// Populado pelo <see cref="JwtAuthStateProvider"/> / <see cref="AuthService"/> e
/// consumido pelo <see cref="ApiClient"/> para anexar o header Authorization.
/// </summary>
public sealed class TokenProvider
{
    public string? Token { get; set; }

    public bool HasToken => !string.IsNullOrWhiteSpace(Token);
}
