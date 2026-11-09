using System.Security.Claims;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
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
    TimeProvider clock, IOptions<AEDV02PilotSettings> settings, ITelemetryEventRepository telemetry) : ControllerBase
{
    [HttpGet("confirmed-revision")]
    public async Task<IActionResult> ConfirmedRevision(Guid matchId, CancellationToken ct)
    {
        if (!TryUser(out var user)) return Unauthorized();
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (binding == null) return NotFound();
        if (binding.HostUserId != user) return StatusCode(403);
        if (binding.BalanceBuildVersion == null || binding.ResolutionMode != "ADAPTIVE" ||
            binding.Status != MatchAuthorityStatus.InMatch || binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            return Conflict();
        var row = await db.AEDV02Decisions.AsNoTracking().Where(x => x.MatchId == matchId &&
            x.ApplyStatus == "APPLIED" && x.ReceiptId != null && x.StalkerValuesJson != null)
            .OrderByDescending(x => x.PlanRevision).FirstOrDefaultAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { revision = row == null ? null : AEDV02DecisionService.Map(row, true) }));
    }
    [HttpGet("configuration")]
    public async Task<IActionResult> Configuration(Guid matchId, CancellationToken ct)
    {
        if (!TryUser(out var user)) return Unauthorized();
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (binding == null) return NotFound();
        if (binding.HostUserId != user) return StatusCode(403);
        if (!settings.Value.Enabled || binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime ||
            binding.Status is not (MatchAuthorityStatus.Lobby or MatchAuthorityStatus.InMatch)) return Conflict();
        var scenario = await db.ScenarioDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, ct);
        if (scenario?.ResolutionMode != ScenarioResolutionMode.Adaptive || scenario.UsedFixedFallback) return Conflict();
        var config = await AdaptiveConfigurationService.ForVersionAsync(db, scenario.PolicyConfigVersion, ct);
        if (config is null) return NotFound();
        return Ok(ApiResponse<object>.Ok(new
        {
            Version = scenario.PolicyConfigVersion,
            Baseline = AdaptiveConfigurationService.Baseline(config).ToArray(),
            Parameters = Enum.GetValues<EchoProtocol.AI.Common.AED.AEDV02Key>()
                .Select(key => new { config.Parameters[key.ToString()].Tiers }),
            config.Pilot.ObservationSeconds, config.Pilot.WindowSeconds, config.Pilot.EvidenceMaxAgeSeconds,
            config.Pilot.ReliefThreshold, config.Pilot.IncreasePressureCeiling, config.Pilot.PressureWeights,
            config.Pilot.TierDwellSeconds, config.Pilot.ProtectionSeconds, config.Pilot.ProtectionCooldownSeconds,
            config.Pilot.ProtectionQuota, settings.Value.CabinetAmbushEnabled
        }));
    }

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
        var config = await AdaptiveConfigurationService.ForVersionAsync(db, scenario.PolicyConfigVersion, ct);
        if (config is not null && scenario.UsedFixedFallback) return Conflict();
        var policy = config?.Pilot ?? AdaptiveConfigurationService.DefaultPilot;
        var result = await sources.VerifyAsync(matchId, request, ct);
        if (!result.IsSuccess) return Respond(result);
        var source = result.Data!;
        var profile = await HistoricalProfile(matchId, source.PlayerPressure.Keys.ToArray(), source.SceneManifestFingerprint, ct);
        var supportRows = await db.AEDV02Decisions.AsNoTracking().Where(x => x.MatchId == matchId && x.ApprovalStatus == "APPROVED" &&
            x.ApplyStatus != "ABORTED" && x.ChangedKey == "SupportTool").ToArrayAsync(ct);
        var supportKinds = supportRows.Select(x => JsonSerializer.Deserialize<AEDV02DecisionRequest>(x.ProposalJson)?.Support?.Kind).ToArray();
        return Ok(ApiResponse<AEDV02SourceRead>.Ok(new(source.Fingerprint, source.PhaseOrdinal, source.StartTick, source.EndTick,
            source.RosterIdentity, source.SceneManifestFingerprint, source.PlayerPressure.Select(x => new AEDV02PlayerPressureRead(x.Key, x.Value,
                source.PressureComponents[x.Key], source.PressureComplete ? EchoProtocol.AI.Common.AED.AEDV02PilotPolicy.Tier(x.Value) : -1, source.PressureEpisodeIds[x.Key])).ToArray(), source.RecentDownUsers,
            source.SafeWindow, source.LogicalPhase, source.Zone, source.PhaseName, source.PlanRevision, source.PlanFingerprint, source.SourceEndedAtUtc,
            source.PressureComplete, source.MissingChannels, source.CabinetClueEventIds, source.TeamPressureStatus,
            source.OpportunityContractVersion, source.TeamPressureTier, source.TeamPressureSeverity, profile.IncreaseAxes, source.ResourceNeeds, source.SupportSafeWindow,
            Math.Max(0, policy.OrdinarySupportQuota - supportKinds.Count(x => x == "ORDINARY")),
            Math.Max(0, policy.EmergencySupportQuota - supportKinds.Count(x => x == "EMERGENCY")),
            supportRows.Length == 0 ? null : supportRows.Max(x => x.CreatedAtUtc).AddSeconds(policy.SupportCooldownSeconds))));
    }
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(Guid matchId, [FromQuery] string sceneFingerprint, CancellationToken ct)
    {
        if (!TryUser(out var user)) return Unauthorized();
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (binding == null) return NotFound();
        if (binding.HostUserId != user) return StatusCode(403);
        if (!settings.Value.Enabled || binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime ||
            binding.Status is not (MatchAuthorityStatus.Lobby or MatchAuthorityStatus.InMatch) ||
            !await db.AEDV02SceneContents.AnyAsync(x => x.Fingerprint == sceneFingerprint, ct) ||
            await db.MatchPlayerBindings.AnyAsync(x => x.MatchId == matchId && x.DisconnectedAtUtc != null, ct)) return Conflict();
        var users = await db.MatchPlayerBindings.Where(x => x.MatchId == matchId).Select(x => x.UserId).ToArrayAsync(ct);
        return Ok(ApiResponse<AEDV02HistoricalProfile>.Ok(await HistoricalProfile(matchId, users, sceneFingerprint, ct)));
    }
    private async Task<AEDV02HistoricalProfile> HistoricalProfile(Guid match, Guid[] users, string scene, CancellationToken ct)
    {
        var axes = new List<string>();
        if (users.Length is >= 1 and <= 4)
            foreach (string axis in EchoProtocol.AI.Common.AED.AEDV02PilotOpportunities.HistoricalIncreaseAxes)
            {
                var history = await new AEDV02HistoricalCompetenceService(db, telemetry, clock).ReadAsync(match, users, scene, axis, ct);
                if (history.Count == users.Length && history.Values.All(x => x.ReadyForIncrease)) axes.Add(axis);
            }
        return new(EchoProtocol.AI.Common.AED.AEDV02PilotOpportunities.Version,
            EchoProtocol.AI.Common.AED.AEDV02PilotOpportunities.ComparisonContext(scene, users.Length), axes.ToArray());
    }
    [HttpPost("decisions")]
    public async Task<IActionResult> Submit(Guid matchId, AEDV02DecisionRequest request, CancellationToken ct) =>
        TryUser(out var user) ? Respond(await decisions.SubmitAsync(user, matchId, request, ct)) : Unauthorized();
    [HttpPut("decisions/{decisionId:guid}/applied")]
    public async Task<IActionResult> Applied(Guid matchId, Guid decisionId, AEDV02ReceiptRequest request, CancellationToken ct) =>
        TryUser(out var user) ? Respond(await decisions.ConfirmAsync(user, matchId, decisionId, request, ct)) : Unauthorized();
    [HttpPut("decisions/{decisionId:guid}/aborted")]
    public async Task<IActionResult> Aborted(Guid matchId, Guid decisionId, CancellationToken ct, bool mayHaveCommitted = false) =>
        TryUser(out var user) ? Respond(await decisions.AbortAsync(user, matchId, decisionId, ct, mayHaveCommitted)) : Unauthorized();

    [HttpPut("decisions/{decisionId:guid}/client-observed")]
    public async Task<IActionResult> ClientObserved(Guid matchId, Guid decisionId, AEDV02ClientObservationRequest request, CancellationToken ct)
    {
        if (!TryUser(out var user)) return Unauthorized();
        var decision = await db.AEDV02Decisions.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId && x.DecisionId == decisionId, ct);
        if (decision is null) return NotFound();
        if (decision.HostUserId == user || !await db.MatchPlayerBindings.AnyAsync(
            x => x.MatchId == matchId && x.UserId == user && x.Participated, ct)) return Forbid();
        if (decision.ApprovalStatus != "APPROVED" || decision.ApplyStatus == "ABORTED" ||
            request.PlanRevision != decision.PlanRevision || request.PlanFingerprint != decision.PlanFingerprint)
            return Conflict();
        var resolved = AEDV02DecisionService.ReadStalkerValues(decision);
        if (resolved != null && (!request.ObservedChaseSpeed.HasValue ||
            !double.IsFinite(request.ObservedChaseSpeed.Value) ||
            Math.Abs(request.ObservedChaseSpeed.Value - resolved[(int)EchoProtocol.AI.Common.AED.AEDV02Key.ChaseSpeed]) >= .0001))
            return Conflict();
        if (AEDV02DecisionService.ReadResolvedValues(decision) is { Length: 13 } effective &&
            (request.ObservedStalkerValues?.Length != 6 || request.ObservedResolvedValues?.Length != 12 ||
             request.ObservedStalkerValues.Where((value, index) => !double.IsFinite(value) || Math.Abs(value - effective[index]) >= .0001).Any() ||
             request.ObservedResolvedValues.Where((value, index) => !double.IsFinite(value) || Math.Abs(value - effective[index]) >= .0001).Any()))
            return Conflict();
        var observations = db.Set<EchoProtocol.Api.Entities.AEDV02ClientObservation>();
        if (!await observations.AnyAsync(x => x.DecisionId == decisionId && x.UserId == user, ct))
        {
            observations.Add(new() { DecisionId = decisionId, UserId = user, ObservedAtUtc = clock.GetUtcNow().UtcDateTime });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation }) { }
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }
    private bool TryUser(out Guid user) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out user) && user != Guid.Empty;
    private IActionResult Respond<T>(ServiceResult<T> result) => result.IsSuccess
        ? Ok(ApiResponse<T>.Ok(result.Data!, result.Message))
        : StatusCode(result.ErrorCode == ErrorCodes.MatchAuthorityForbidden ? 403 : result.ErrorCode == ErrorCodes.ValidationError ? 400 : 409,
            ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
}

public sealed record AEDV02ClientObservationRequest(int PlanRevision, string PlanFingerprint, double? ObservedChaseSpeed = null,
    double[]? ObservedStalkerValues = null, double[]? ObservedResolvedValues = null);
