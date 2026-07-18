using ConexaoSolidaria.Shared.Domain;

namespace ConexaoSolidaria.Tests;

/// <summary>
/// Cobre as garantias de transicao de estado do dominio de doacao (sem banco):
/// estado inicial Pendente, transicao unica e imutabilidade do valor.
/// </summary>
public sealed class DonationDomainTests
{
    private static Donation NovaDoacao(decimal valor = 150m) =>
        Donation.Create(Guid.NewGuid(), Guid.NewGuid(), "Doador@Exemplo.COM", valor);

    [Fact]
    public void Create_ShouldStartAsPendente()
    {
        var donation = NovaDoacao();

        Assert.Equal(DonationStatus.Pendente, donation.Status);
        Assert.Null(donation.ProcessadaEm);
    }

    [Fact]
    public void Create_ShouldNormalizeEmailAndKeepValor()
    {
        var donation = NovaDoacao(250.50m);

        Assert.Equal("doador@exemplo.com", donation.DoadorEmail);
        Assert.Equal(250.50m, donation.Valor);
    }

    [Fact]
    public void Create_ShouldRejectNonPositiveValor()
    {
        Assert.Throws<DomainRuleException>(() => NovaDoacao(0m));
        Assert.Throws<DomainRuleException>(() => NovaDoacao(-10m));
    }

    [Fact]
    public void MarkAsProcessed_ShouldMoveFromPendenteToProcessada()
    {
        var donation = NovaDoacao();
        var now = DateTimeOffset.UtcNow;

        donation.MarkAsProcessed(now);

        Assert.Equal(DonationStatus.Processada, donation.Status);
        Assert.Equal(now.ToUniversalTime(), donation.ProcessadaEm);
    }

    [Fact]
    public void MarkAsRejected_ShouldMoveFromPendenteToRejeitada()
    {
        var donation = NovaDoacao();
        var now = DateTimeOffset.UtcNow;

        donation.MarkAsRejected(now);

        Assert.Equal(DonationStatus.Rejeitada, donation.Status);
        Assert.Equal(now.ToUniversalTime(), donation.ProcessadaEm);
    }

    [Fact]
    public void MarkAsProcessed_CalledTwice_ShouldThrowAndKeepState()
    {
        var donation = NovaDoacao();
        var processedAt = DateTimeOffset.UtcNow;
        donation.MarkAsProcessed(processedAt);

        var exception = Assert.Throws<DomainRuleException>(
            () => donation.MarkAsProcessed(DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.Contains("finalizada", exception.Message);
        // Estado permanece intacto apos a segunda transicao rejeitada.
        Assert.Equal(DonationStatus.Processada, donation.Status);
        Assert.Equal(processedAt.ToUniversalTime(), donation.ProcessadaEm);
    }

    [Fact]
    public void MarkAsRejected_AfterProcessed_ShouldThrow()
    {
        var donation = NovaDoacao();
        donation.MarkAsProcessed(DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleException>(
            () => donation.MarkAsRejected(DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.Equal(DonationStatus.Processada, donation.Status);
    }

    [Fact]
    public void MarkAsProcessed_AfterRejected_ShouldThrow()
    {
        var donation = NovaDoacao();
        donation.MarkAsRejected(DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleException>(
            () => donation.MarkAsProcessed(DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.Equal(DonationStatus.Rejeitada, donation.Status);
    }
}
