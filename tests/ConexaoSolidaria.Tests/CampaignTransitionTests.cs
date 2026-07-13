using ConexaoSolidaria.Shared.Domain;

namespace ConexaoSolidaria.Tests;

/// <summary>
/// Complementa <see cref="CampaignRuleTests"/> cobrindo a rejeicao de doacao para campanhas
/// concluidas e para campanhas ativas com o periodo ja encerrado (DataFim no passado).
/// </summary>
public sealed class CampaignTransitionTests
{
    private static Campaign CampanhaAtiva(DateTimeOffset now) =>
        Campaign.Create(
            "Natal Solidario",
            "Arrecadacao para criancas",
            now.AddDays(-1),
            now.AddDays(10),
            1000,
            CampaignStatus.Ativa,
            now);

    [Fact]
    public void CanReceiveDonation_ShouldRejectConcludedCampaign()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = Campaign.Create(
            "Natal Solidario",
            "Arrecadacao para criancas",
            now,
            now.AddDays(10),
            1000,
            CampaignStatus.Concluida,
            now);

        Assert.False(campaign.CanReceiveDonation(now));
    }

    [Fact]
    public void CanReceiveDonation_ShouldRejectActiveCampaignAfterEndDate()
    {
        var creationTime = DateTimeOffset.UtcNow;
        var campaign = CampanhaAtiva(creationTime);

        // Avanca o relogio para depois da DataFim: mesmo Ativa, nao pode mais receber doacao.
        var afterEnd = creationTime.AddDays(11);

        Assert.False(campaign.CanReceiveDonation(afterEnd));
    }

    [Fact]
    public void CanReceiveDonation_ShouldRejectExactlyAfterEndDate()
    {
        var creationTime = DateTimeOffset.UtcNow;
        var campaign = CampanhaAtiva(creationTime);

        // Um tick apos a DataFim ja invalida (regra usa DataFim >= now).
        var justAfterEnd = campaign.DataFim.AddTicks(1);

        Assert.False(campaign.CanReceiveDonation(justAfterEnd));
    }
}
