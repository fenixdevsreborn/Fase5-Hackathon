namespace ConexaoSolidaria.Web.Services.Ai;

/// <summary>
/// Configuracao das features de IA (Assistente Solidario + criacao assistida de campanhas).
/// A chave vem da env OPENAI_API_KEY (nunca de arquivo versionado); modelo e limites vem
/// da secao "Ai" do appsettings. Sem chave configurada o app sobe normalmente e a UI
/// esconde as features (degradacao graciosa, mesmo espirito do RabbitMQ opcional).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Chave da API OpenAI. Populada via env OPENAI_API_KEY no Program.cs.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Modelo de chat usado pelo agente e pela criacao assistida.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Limite de tokens de saida por resposta (controle de custo/latencia).</summary>
    public int MaxOutputTokens { get; set; } = 800;

    /// <summary>Indica se as features de IA estao habilitadas (chave presente).</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
