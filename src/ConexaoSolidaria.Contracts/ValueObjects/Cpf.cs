using ConexaoSolidaria.Contracts.Validation;

namespace ConexaoSolidaria.Contracts.ValueObjects;

/// <summary>
/// Value Object de CPF. Normaliza (apenas digitos) e valida os digitos verificadores na
/// construcao, garantindo que um <see cref="Cpf"/> so exista em estado valido no dominio.
/// Reaproveita o algoritmo de <see cref="CpfValidator"/> (IsValid/Normalize/Mask) — nao duplica logica.
/// </summary>
public readonly record struct Cpf
{
    private Cpf(string value) => Value = value;

    /// <summary>CPF normalizado com exatamente 11 digitos, sem pontuacao.</summary>
    public string Value { get; }

    /// <summary>
    /// Representacao mascarada para exibicao/log, revelando apenas os 2 ultimos digitos:
    /// "***.***.***-NN". Nunca expoe o CPF completo.
    /// </summary>
    public string Masked => CpfValidator.Mask(Value);

    /// <summary>
    /// Cria um <see cref="Cpf"/> valido a partir de uma string com ou sem pontuacao.
    /// </summary>
    /// <exception cref="ArgumentException">Quando o CPF e nulo/vazio ou possui digitos verificadores invalidos.</exception>
    public static Cpf Create(string? cpf)
    {
        if (!CpfValidator.IsValid(cpf))
        {
            throw new ArgumentException("CPF invalido.", nameof(cpf));
        }

        return new Cpf(CpfValidator.Normalize(cpf!));
    }

    /// <summary>
    /// Tenta criar um <see cref="Cpf"/> valido. Retorna <c>false</c> sem lancar excecao
    /// quando o CPF e invalido.
    /// </summary>
    public static bool TryParse(string? cpf, out Cpf result)
    {
        if (CpfValidator.IsValid(cpf))
        {
            result = new Cpf(CpfValidator.Normalize(cpf!));
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Retorna o CPF mascarado, nunca o valor completo, para evitar vazamento em logs.</summary>
    public override string ToString() => Masked;

    public static implicit operator string(Cpf cpf) => cpf.Value;
}
