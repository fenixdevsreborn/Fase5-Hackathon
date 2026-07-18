namespace ConexaoSolidaria.IntegrationTests.Infrastructure;

/// <summary>
/// Gera CPFs sinteticos VALIDOS (digitos verificadores corretos) para os cadastros nos testes.
/// Usa o mesmo algoritmo de digito verificador do <c>CpfValidator</c> do dominio.
/// </summary>
public static class CpfGenerator
{
    private static int _seed = Random.Shared.Next(100_000_000, 999_999_999);

    public static string Next()
    {
        // Sequencial para garantir unicidade entre chamadas dentro do mesmo processo de teste.
        var basis = Interlocked.Increment(ref _seed) % 1_000_000_000;
        var nineDigits = basis.ToString("D9");

        var d1 = CalculateVerifier(nineDigits, 10);
        var d2 = CalculateVerifier(nineDigits + d1, 11);

        return $"{nineDigits}{d1}{d2}";
    }

    private static int CalculateVerifier(string digits, int initialWeight)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            sum += (digits[i] - '0') * (initialWeight - i);
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
