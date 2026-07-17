using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ConexaoSolidaria.Web.Services.Ai;

/// <summary>Mensagem exibida no chat do Assistente Solidario.</summary>
public sealed class ChatMessageVm
{
    public required string Role { get; init; } // "user" | "assistant"
    public string Text { get; set; } = string.Empty;
    public bool IsError { get; set; }
}

/// <summary>
/// Servico do chat do Assistente Solidario (Feature: chatbot para doadores).
/// Scoped por circuito Blazor: o <see cref="AgentSession"/> guarda o historico da conversa
/// da sessao atual (refresh/reconexao = conversa nova; aceitavel para a POC).
///
/// O agente e criado lazy no primeiro envio (nunca no prerender) e as tools fecham sobre
/// o <see cref="AssistantTools"/> scoped, que ja propaga o JWT do usuario logado.
/// Erros de runtime (chave invalida, 429, timeout) viram mensagem amigavel no chat —
/// nunca propagam para o circuito.
/// </summary>
public sealed class AssistantChatService(
    AiChatClientProvider provider,
    AssistantTools tools,
    ILogger<AssistantChatService> logger)
{
    private const string Instrucoes =
        """
        Voce e o Assistente Solidario da plataforma Conexao Solidaria, que conecta doadores
        a campanhas de ONGs. Responda SEMPRE em portugues do Brasil, em texto simples
        (sem markdown, sem tabelas), de forma curta, calorosa e objetiva.

        Voce pode: buscar campanhas ativas, detalhar uma campanha, mostrar estatisticas de
        transparencia (quanto cada campanha arrecadou) e consultar as doacoes do usuario
        logado. Use as ferramentas disponiveis para responder com dados reais — nunca
        invente campanhas, valores ou status. Valores monetarios sao em reais (R$).

        Se o usuario nao estiver logado e pedir dados pessoais (minhas doacoes), oriente-o
        a entrar na conta. Se perguntarem algo fora do tema doacoes/campanhas/plataforma,
        redirecione gentilmente para o proposito do assistente.
        """;

    private readonly List<ChatMessageVm> _mensagens = [];
    private AIAgent? _agent;
    private AgentSession? _session;

    public bool Enabled => provider.Enabled;

    public IReadOnlyList<ChatMessageVm> Mensagens => _mensagens;

    public bool Ocupado { get; private set; }

    /// <summary>
    /// Envia a mensagem do usuario e faz stream da resposta do agente.
    /// <paramref name="onUpdate"/> e invocado a cada chunk para o componente re-renderizar
    /// (o componente decide o throttle).
    /// </summary>
    public async Task EnviarAsync(string texto, Func<Task>? onUpdate = null, CancellationToken ct = default)
    {
        if (!Enabled || Ocupado || string.IsNullOrWhiteSpace(texto))
        {
            return;
        }

        Ocupado = true;
        _mensagens.Add(new ChatMessageVm { Role = "user", Text = texto.Trim() });
        var resposta = new ChatMessageVm { Role = "assistant" };
        _mensagens.Add(resposta);

        try
        {
            _agent ??= CriarAgente();
            _session ??= await _agent.CreateSessionAsync(ct);

            await foreach (var update in _agent.RunStreamingAsync(texto.Trim(), _session, cancellationToken: ct))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                resposta.Text += update.Text;
                if (onUpdate is not null)
                {
                    await onUpdate();
                }
            }

            if (string.IsNullOrWhiteSpace(resposta.Text))
            {
                resposta.Text = "Desculpe, nao consegui elaborar uma resposta. Pode reformular a pergunta?";
            }
        }
        catch (OperationCanceledException)
        {
            _mensagens.Remove(resposta);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha na chamada ao Assistente Solidario (IA).");
            resposta.Text = "O assistente esta indisponivel no momento. Tente novamente em instantes.";
            resposta.IsError = true;
        }
        finally
        {
            Ocupado = false;
            if (onUpdate is not null)
            {
                await onUpdate();
            }
        }
    }

    private AIAgent CriarAgente() =>
        provider.GetClient().AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AssistenteSolidario",
            ChatOptions = new ChatOptions
            {
                Instructions = Instrucoes,
                MaxOutputTokens = provider.Options.MaxOutputTokens,
                Tools =
                [
                    AIFunctionFactory.Create(tools.BuscarCampanhasAsync),
                    AIFunctionFactory.Create(tools.ObterCampanhaAsync),
                    AIFunctionFactory.Create(tools.ObterEstatisticasAsync),
                    AIFunctionFactory.Create(tools.MinhasDoacoesAsync),
                ],
            },
        });
}
