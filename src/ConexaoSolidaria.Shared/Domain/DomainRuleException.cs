namespace ConexaoSolidaria.Shared.Domain;

public sealed class DomainRuleException(string message) : Exception(message);
