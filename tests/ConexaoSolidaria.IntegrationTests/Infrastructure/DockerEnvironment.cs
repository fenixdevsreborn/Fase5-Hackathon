using System.Diagnostics;

namespace ConexaoSolidaria.IntegrationTests.Infrastructure;

/// <summary>
/// Deteccao (cacheada) da disponibilidade do Docker. Usada para SKIPAR graciosamente os testes de
/// integracao quando o daemon nao esta acessivel, em vez de falhar a suite. A checagem roda uma unica
/// vez por processo de teste ("docker version") com timeout curto.
/// </summary>
public static class DockerEnvironment
{
    public const string SkipReason =
        "Docker nao esta disponivel neste ambiente; testes de integracao com Testcontainers foram ignorados.";

    private static readonly Lazy<bool> Available = new(Detect, isThreadSafe: true);

    public static bool IsAvailable => Available.Value;

    private static bool Detect()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "version --format {{.Server.Version}}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return false;
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }

                return false;
            }

            // Exit 0 => CLI conseguiu falar com o daemon (server version resolvida).
            return process.ExitCode == 0;
        }
        catch
        {
            // docker CLI ausente / nao inicializavel.
            return false;
        }
    }
}
