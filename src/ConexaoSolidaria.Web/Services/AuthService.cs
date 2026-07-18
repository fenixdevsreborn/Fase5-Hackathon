using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ConexaoSolidaria.Web.Services;

/// <summary>
/// Orquestra login / cadastro / logout: chama a API, persiste o JWT no
/// ProtectedLocalStorage, atualiza o <see cref="TokenProvider"/> e notifica o
/// <see cref="JwtAuthStateProvider"/> para reavaliar o estado de autenticacao.
/// </summary>
public sealed class AuthService(
    ApiClient api,
    ProtectedLocalStorage storage,
    AuthenticationStateProvider authStateProvider,
    TokenProvider tokenProvider)
{
    public async Task<AuthResult?> LoginAsync(string email, string senha, CancellationToken ct = default)
    {
        var result = await api.LoginAsync(email, senha, ct);
        return await PersistAsync(result);
    }

    public async Task<AuthResult?> CadastrarDoadorAsync(CadastroDoador dto, CancellationToken ct = default)
    {
        var result = await api.CadastrarDoadorAsync(dto, ct);
        return await PersistAsync(result);
    }

    public async Task LogoutAsync()
    {
        await storage.DeleteAsync(JwtAuthStateProvider.TokenKey);
        tokenProvider.Token = null;
        (authStateProvider as JwtAuthStateProvider)?.NotifyUserLogout();
    }

    private async Task<AuthResult?> PersistAsync(AuthResult? result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            return null;
        }

        await storage.SetAsync(JwtAuthStateProvider.TokenKey, result.AccessToken);
        tokenProvider.Token = result.AccessToken;
        (authStateProvider as JwtAuthStateProvider)?.NotifyUserAuthentication(result.AccessToken);
        return result;
    }
}
