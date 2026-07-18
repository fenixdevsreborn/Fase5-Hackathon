namespace ConexaoSolidaria.Contracts.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "ConexaoSolidaria";

    public string Audience { get; init; } = "ConexaoSolidaria";

    public string Secret { get; init; } = string.Empty;

    public int ExpiresMinutes { get; init; } = 120;
}
