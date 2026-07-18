using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Asp.Versioning;
using ConexaoSolidaria.Shared.Domain;
using ConexaoSolidaria.Shared.Persistence;
using ConexaoSolidaria.Campaigns.Api.Infrastructure;
using ConexaoSolidaria.Campaigns.Api.Requests;
using ConexaoSolidaria.Campaigns.Api.Responses;
using ConexaoSolidaria.Contracts.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prometheus;

namespace ConexaoSolidaria.Campaigns.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/doacoes")]
public sealed class DoacoesController(CampaignsDbContext db) : ControllerBase
{
    private static readonly Counter RejectedDonations = Metrics.CreateCounter(
        "conexao_donations_rejected_total",
        "Quantidade de tentativas de doacao rejeitadas por campanha encerrada ou cancelada.");

    [HttpPost]
    [Authorize(Policy = "DonationCreation")]
    [ProducesResponseType<DoacaoAceitaResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DoacaoAceitaResponse>> Criar(
        CriarDoacaoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var doadorIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var doadorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        if (!Guid.TryParse(doadorIdValue, out var doadorId))
        {
            return ProblemResults.Unauthorized("Token JWT nao contem o identificador do doador.");
        }

        var hasIdempotencyKey = !string.IsNullOrWhiteSpace(idempotencyKey);
        if (hasIdempotencyKey)
        {
            idempotencyKey = idempotencyKey!.Trim();

            // Reapresentacao: se a chave ja foi vista, devolve a MESMA doacao (202) sem recriar.
            var replay = await FindDonationByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (replay is not null)
            {
                return Accepted(ToAceitaResponse(replay));
            }
        }

        if (request.ValorDoacao <= 0)
        {
            return ProblemResults.UnprocessableEntity("ValorDoacao deve ser maior que zero.");
        }

        var campaign = await db.Campaigns.SingleOrDefaultAsync(
            item => item.Id == request.IdCampanha,
            cancellationToken);

        if (campaign is null)
        {
            return ProblemResults.NotFound("Campanha nao encontrada.");
        }

        if (!campaign.CanReceiveDonation(DateTimeOffset.UtcNow))
        {
            RejectedDonations.Inc();
            return ProblemResults.UnprocessableEntity(
                "Doacao nao permitida para campanhas encerradas ou canceladas.");
        }

        var donation = Donation.Create(campaign.Id, doadorId, doadorEmail, request.ValorDoacao);
        db.Donations.Add(donation);

        // Padrao Outbox: o evento e persistido na MESMA transacao (unico SaveChanges) que a
        // doacao. Elimina o dual-write; a publicacao no broker fica a cargo do dispatcher.
        var correlationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        var donationEvent = new DoacaoRecebidaEvent(
            Guid.NewGuid(),
            donation.Id,
            campaign.Id,
            doadorId,
            doadorEmail,
            donation.Valor,
            DateTimeOffset.UtcNow,
            correlationId);

        var payload = JsonSerializer.Serialize(donationEvent);

        var outboxMessage = OutboxMessage.Create(
            eventType: nameof(DoacaoRecebidaEvent),
            schemaVersion: donationEvent.SchemaVersion,
            payload: payload,
            correlationId: correlationId);

        db.OutboxMessages.Add(outboxMessage);

        // A linha de idempotencia e gravada na MESMA transacao (unico SaveChanges) que a doacao e a
        // outbox message, garantindo atomicidade: ou tudo persiste, ou nada.
        if (hasIdempotencyKey)
        {
            db.DonationIdempotencyKeys.Add(DonationIdempotencyKey.Create(idempotencyKey!, donation.Id));
        }

        try
        {
            // Atomico: doacao + outbox + idempotency key gravadas juntas na transacao do SaveChanges.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (hasIdempotencyKey)
        {
            // Corrida: outra requisicao gravou a mesma Idempotency-Key primeiro (violacao de unique).
            // Descarta o estado pendente, recarrega e devolve a doacao vencedora.
            db.ChangeTracker.Clear();

            var winner = await FindDonationByIdempotencyKeyAsync(idempotencyKey!, cancellationToken);
            if (winner is not null)
            {
                return Accepted(ToAceitaResponse(winner));
            }

            throw;
        }

        return Accepted(ToAceitaResponse(donation));
    }

    private async Task<Donation?> FindDonationByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var stored = await db.DonationIdempotencyKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(key => key.Key == idempotencyKey, cancellationToken);

        if (stored is null)
        {
            return null;
        }

        return await db.Donations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == stored.DonationId, cancellationToken);
    }

    private static DoacaoAceitaResponse ToAceitaResponse(Donation donation)
    {
        return new DoacaoAceitaResponse(
            donation.Id,
            donation.CampaignId,
            donation.Valor,
            donation.Status.ToString(),
            "Doacao recebida e enviada para processamento assincrono.");
    }

    // Consulta de status usada pelo frontend para acompanhar o processamento assincrono da doacao.
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "DonationCreation")]
    [ProducesResponseType<DoacaoStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoacaoStatusResponse>> ObterStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        var doadorIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(doadorIdValue, out var doadorId))
        {
            return ProblemResults.Unauthorized("Token JWT nao contem o identificador do doador.");
        }

        // Projecao com join na campanha para trazer o titulo e as datas do comprovante.
        var status = await db.Donations
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.CampaignId,
                item.DoadorId,
                item.Valor,
                item.Status,
                item.CriadaEm,
                item.ProcessadaEm,
                CampanhaTitulo = item.Campaign!.Titulo
            })
            .SingleOrDefaultAsync(cancellationToken);

        // 404 tambem quando a doacao pertence a outro doador: nao revela existencia de recursos alheios.
        if (status is null || status.DoadorId != doadorId)
        {
            return ProblemResults.NotFound("Doacao nao encontrada.");
        }

        return Ok(new DoacaoStatusResponse(
            status.Id,
            status.CampaignId,
            status.Valor,
            status.Status.ToString(),
            status.CampanhaTitulo,
            status.CriadaEm,
            status.ProcessadaEm));
    }

    // Lista as doacoes do doador autenticado (com o titulo da campanha), da mais recente para a mais antiga.
    [HttpGet("minhas")]
    [Authorize(Policy = "DonationCreation")]
    [ProducesResponseType<IReadOnlyCollection<MinhaDoacaoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<MinhaDoacaoResponse>>> Minhas(
        CancellationToken cancellationToken)
    {
        var doadorIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(doadorIdValue, out var doadorId))
        {
            return ProblemResults.Unauthorized("Token JWT nao contem o identificador do doador.");
        }

        var doacoes = await db.Donations
            .AsNoTracking()
            .Where(item => item.DoadorId == doadorId)
            .OrderByDescending(item => item.CriadaEm)
            .Select(item => new MinhaDoacaoResponse(
                item.Id,
                item.CampaignId,
                item.Campaign!.Titulo,
                item.Valor,
                item.Status.ToString(),
                item.CriadaEm,
                item.ProcessadaEm))
            .ToListAsync(cancellationToken);

        return Ok(doacoes);
    }
}
