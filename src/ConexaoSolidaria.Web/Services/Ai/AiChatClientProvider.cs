using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace ConexaoSolidaria.Web.Services.Ai;

/// <summary>
/// Singleton que expoe o <see cref="IChatClient"/> (OpenAI) compartilhado pelos agentes.
///
/// Decisoes:
/// - Lazy: o SDK so e instanciado no primeiro uso e somente se houver chave configurada.
/// - NAO usa IHttpClientFactory de proposito: o AddServiceDefaults aplica
///   AddStandardResilienceHandler() (timeout ~30s + retries) a todos os clients da factory,
///   pessimo para chamadas de LLM (retry duplicaria custo/latencia). O SDK OpenAI usa o
///   transporte proprio com NetworkTimeout explicito.
/// </summary>
public sealed class AiChatClientProvider(IOptions<AiOptions> options)
{
    private readonly Lazy<IChatClient> _client = new(() =>
    {
        var opts = options.Value;
        var openAi = new OpenAIClient(
            new ApiKeyCredential(opts.ApiKey!),
            new OpenAIClientOptions { NetworkTimeout = TimeSpan.FromSeconds(60) });

        return openAi.GetChatClient(opts.Model).AsIChatClient();
    });

    public AiOptions Options { get; } = options.Value;

    /// <summary>Features de IA habilitadas (OPENAI_API_KEY presente).</summary>
    public bool Enabled => Options.IsConfigured;

    /// <summary>Cliente de chat compartilhado. Lanca se as features estiverem desabilitadas.</summary>
    public IChatClient GetClient() =>
        Enabled
            ? _client.Value
            : throw new InvalidOperationException(
                "IA desabilitada: configure a variavel de ambiente OPENAI_API_KEY.");
}
