namespace ConexaoSolidaria.Campaigns.Api.Requests;

public sealed class TransparenciaCampanhasQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public string? Titulo { get; init; }

    public decimal? MetaMinima { get; init; }

    public decimal? MetaMaxima { get; init; }

    public decimal? ValorArrecadadoMinimo { get; init; }

    public decimal? ValorArrecadadoMaximo { get; init; }

    public DateTimeOffset? DataFimInicial { get; init; }

    public DateTimeOffset? DataFimFinal { get; init; }

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (Page < 1)
        {
            errors[nameof(Page)] = ["page deve ser maior ou igual a 1."];
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            errors[nameof(PageSize)] = [$"pageSize deve estar entre 1 e {MaxPageSize}."];
        }

        if (MetaMinima < 0)
        {
            errors[nameof(MetaMinima)] = ["metaMinima deve ser maior ou igual a zero."];
        }

        if (MetaMaxima < 0)
        {
            errors[nameof(MetaMaxima)] = ["metaMaxima deve ser maior ou igual a zero."];
        }

        if (MetaMinima > MetaMaxima)
        {
            errors["MetaFinanceira"] = ["metaMinima nao pode ser maior que metaMaxima."];
        }

        if (ValorArrecadadoMinimo < 0)
        {
            errors[nameof(ValorArrecadadoMinimo)] = ["valorArrecadadoMinimo deve ser maior ou igual a zero."];
        }

        if (ValorArrecadadoMaximo < 0)
        {
            errors[nameof(ValorArrecadadoMaximo)] = ["valorArrecadadoMaximo deve ser maior ou igual a zero."];
        }

        if (ValorArrecadadoMinimo > ValorArrecadadoMaximo)
        {
            errors["ValorArrecadado"] = ["valorArrecadadoMinimo nao pode ser maior que valorArrecadadoMaximo."];
        }

        if (DataFimInicial > DataFimFinal)
        {
            errors["DataFim"] = ["dataFimInicial nao pode ser maior que dataFimFinal."];
        }

        return errors;
    }
}
