using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Controllers;

[ApiController, Authorize, Route("api/matches/{matchId:guid}/aed-v02")]
public sealed class AEDV02Controller(AppDbContext db, AEDV02SourceVerifier sources, AEDV02DecisionService decisions,
    TimeProvider clock, IOptions<AEDV02PilotSettings> settings) : ControllerBase
{
    [HttpPost("sources")]
    public async Task<IActionResult> Sources(Guid matchId, AEDV02SourceRequest request, CancellationToken ct)
    {
        if (!TryUser(out var user)) return Unauthorized();
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (binding == null) return NotFound();
        if (binding.HostUserId != user) return StatusCode(403);
        if (binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) return StatusCode(410);
        if (!settings.Value.Enabled || binding.Status != MatchAuthorityStatus.InMatch) return Conflict();
        var scenario = await db.ScenarioDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, ct);
        if (scenario?.ResolutionMode != ScenarioResolutionMode.Adaptive) return Conflict();
        var result = await sources.VerifyAsync(matchId, request, ct);
        if (!result.IsSuccess) return Respond(result);
        var source = result.Data!;
        return Ok(ApiResponse<AEDV02SourceRead>.Ok(new(source.Fingerprint, source.PhaseOrdinal, source.StartTick, source.EndTick,
            source.RosterIdentity, source.SceneManifestFingerprint, source.PlayerPressure.Select(x => new AEDV02PlayerPressureRead(x.Key, x.Value,
                source.PressureComponents[x.Key], EchoProtocol.AI.Common.AED.AEDV02PilotPolicy.Tier(x.Value), source.PressureEpisodeIds[x.Key])).ToArray(), source.RecentDownUsers,
            source.SafeWindow, source.LogicalPhase, source.Zone, source.PhaseName, source.PlanRevision, source.PlanFingerprint, source.SourceEndedAtUtc)));
    }
    [HttpPost("decisions")]
    public async Task<IActionResult> Submit(Guid matchId, AEDV02DecisionRequest request, CancellationToken ct) =>
        TryUser(out var user) ? Respond(await decisions.SubmitAsync(user, matchId, request, ct)) : Unauthorized();
    [HttpPut("decisions/{decisionId:guid}/applied")]
    public async Task<IActionResult> Applied(Guid matchId, Guid decisionId, AEDV02ReceiptRequest request, CancellationToken ct) =>
        TryUser(out var user) ? Respond(await decisions.ConfirmAsync(user, matchId, decisionId, request, ct)) : Unauthorized();
    [HttpPut("decisions/{decisionId:guid}/aborted")]
    public async Task<IActionResult> Aborted(Guid matchId, Guid decisionId, CancellationToken ct) =>
        TryUser(out var user) ? Respond(await decisions.AbortAsync(user, matchId, decisionId, ct)) : Unauthorized();
    private bool TryUser(out Guid user) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out user) && user != Guid.Empty;
    private IActionResult Respond<T>(ServiceResult<T> result) => result.IsSuccess
        ? Ok(ApiResponse<T>.Ok(result.Data!, result.Message))
        : StatusCode(result.ErrorCode == ErrorCodes.MatchAuthorityForbidden ? 403 : result.ErrorCode == ErrorCodes.ValidationError ? 400 : 409,
            ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
}
