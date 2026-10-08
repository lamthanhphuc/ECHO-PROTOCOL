using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class ScenarioSnapshotReadService(AppDbContext db, TimeProvider timeProvider)
    : IScenarioSnapshotReadService
{
    public async Task<ServiceResult<AdaptiveInputSnapshotReadResponse>> GetAsync(
        Guid callerUserId, Guid matchId, Guid decisionId,
        CancellationToken cancellationToken = default)
    {
        ServiceResult<AdaptiveInputSnapshotReadResponse> Fail(string message, string code) =>
            ServiceResult<AdaptiveInputSnapshotReadResponse>.Failure(message, code);

        var match = await db.MatchAuthorityBindings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MatchId == matchId, cancellationToken);
        if (match is null) return Fail("Match was not found", ErrorCodes.MatchNotFound);
        if (match.HostUserId != callerUserId)
            return Fail("Only the verified host may read scenario snapshots", ErrorCodes.MatchAuthorityForbidden);
        if (match.LeaseExpiresAtUtc < timeProvider.GetUtcNow().UtcDateTime)
            return Fail("Host authority lease expired", ErrorCodes.MatchLeaseExpired);

        var decision = await db.ScenarioDecisions.AsNoTracking()
            .Include(x => x.Snapshot).ThenInclude(x => x.Players)
            .SingleOrDefaultAsync(x => x.DecisionId == decisionId && x.MatchId == matchId, cancellationToken);
        if (decision is null)
            return Fail("Scenario decision was not found", ErrorCodes.ScenarioDecisionNotFound);
        if (!decision.IsCurrent || decision.Snapshot.SnapshotId != decision.SnapshotId)
            return Fail("Scenario decision is stale", ErrorCodes.ScenarioDecisionStaleRoster);

        var s = decision.Snapshot;
        var ids = await db.MatchPlayerBindings.AsNoTracking()
            .Where(x => x.MatchId == matchId).Select(x => x.UserId)
            .OrderBy(x => x).ToArrayAsync(cancellationToken);
        var rosterCurrent = ids.Length == s.TeamSize && ids.ToHashSet().SetEquals(s.Players.Select(x => x.UserId))
            && s.RosterIdentity == ScenarioFingerprint.Hash(matchId.ToString("D"),
                string.Join(",", ids.Select(x => x.ToString("D"))));
        var profiles = await db.PlayerAIProfiles.AsNoTracking()
            .Where(x => ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, cancellationToken);
        var revisionsCurrent = s.Players.All(x =>
            profiles.TryGetValue(x.UserId, out var current)
                ? x.ProfileAvailable && x.ProfileRevision == current.ProfileRevision
                    && x.ProfileLineageId == current.ProfileLineageId
                    && x.ProfileFormulaVersion == current.ProfileFormulaVersion
                : !x.ProfileAvailable);
        var fingerprintValid = s.SnapshotContentFingerprint == ScenarioFingerprint.Snapshot(s);
        var available = s.Players.Where(x => x.ProfileAvailable).ToArray();
        var semanticsSupported = available.All(x =>
            x.ProfileFormulaVersion == AdaptiveInputSnapshotBuilder.SupportedProfileFormula)
            && (available.Length == 0 ? s.ProfileFormulaSemanticId is null
                : s.ProfileFormulaSemanticId == AdaptiveInputSnapshotBuilder.SupportedProfileFormula);
        if (!rosterCurrent || !revisionsCurrent || !fingerprintValid || !semanticsSupported)
            return Fail("Scenario snapshot is no longer current or supported", ErrorCodes.ScenarioDecisionStaleRoster);

        return ServiceResult<AdaptiveInputSnapshotReadResponse>.Success(new(
            s.SnapshotId, s.MatchId, s.DecisionPoint, s.DecisionPoint,
            s.SnapshotContentFingerprint, s.RosterIdentity, s.TeamSize,
            s.Validity.ToString().ToUpperInvariant(),
            JsonSerializer.Deserialize<string[]>(s.ReasonCodesJson) ?? [], s.CreatedAtUtc,
            s.FingerprintVersion,
            s.ProfileFormulaSemanticId, s.SurvivalComparisonKey, s.NoiseComparisonKey,
            s.SurvivalAggregationStatus, s.NoiseAggregationStatus,
            s.SurvivalMeanObservedScore, s.NoiseMeanObservedScore,
            s.SurvivalObservedActiveCount, s.NoiseObservedActiveCount,
            s.ObjectiveAggregationStatus, s.ObjectiveComparisonKey,
            s.ObjectiveMeanObservedScore, s.ObjectiveObservedActiveCount,
            s.ToolUsageAggregationStatus, s.ToolUsageComparisonKey,
            s.ToolUsageMeanObservedScore, s.ToolUsageObservedActiveCount,
            s.Players.OrderBy(x => x.UserId).Select(MapPlayer).ToArray(),
            rosterCurrent, revisionsCurrent, fingerprintValid, semanticsSupported,
            s.MatchId == matchId, decision.IsCurrent && s.DecisionPoint == decision.DecisionPoint,
            s.DecisionPoint == "PRE_MATCH"));
    }

    private static AdaptiveInputPlayerReadResponse MapPlayer(
        EchoProtocol.Api.Entities.AdaptiveInputSnapshotPlayer player)
    {
        using var document = JsonDocument.Parse(player.DeferredDimensionsJson);
        var objective = ReadOptional(document.RootElement, "objective");
        var toolUsage = ReadOptional(document.RootElement, "toolUsage");
        return new AdaptiveInputPlayerReadResponse(
            player.UserId, player.ProfileAvailable, player.ProfileLineageId, player.ProfileRevision,
            player.SurvivalScore, player.SurvivalStatus, player.SurvivalSampleCount,
            player.SurvivalComparisonKey, player.NoiseScore, player.NoiseStatus,
            player.NoiseSampleCount, player.NoiseComparisonKey,
            objective.Score, objective.Status, objective.SampleCount, objective.Key,
            toolUsage.Score, toolUsage.Status, toolUsage.SampleCount, toolUsage.Key);
    }

    private static (decimal? Score, string Status, int SampleCount, string? Key) ReadOptional(
        JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return (null, "DEFERRED", 0, null);
        return (
            value.TryGetProperty("score", out var score) && score.ValueKind == JsonValueKind.Number
                ? score.GetDecimal() : null,
            value.TryGetProperty("status", out var status) ? status.GetString() ?? "DEFERRED" : "DEFERRED",
            value.TryGetProperty("sampleCount", out var count) ? count.GetInt32() : 0,
            value.TryGetProperty("comparisonKey", out var key) && key.ValueKind == JsonValueKind.String
                ? key.GetString() : null);
    }
}
