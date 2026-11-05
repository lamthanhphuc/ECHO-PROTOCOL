using System.Globalization;
using System.Text.Json;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AdaptiveInputSnapshotBuilder(AppDbContext db, TimeProvider timeProvider)
    : IAdaptiveInputSnapshotBuilder
{
    private const string SupportedProfileFormula = "PROFILE_FORMULA_V1_1";
    private const string DeferredJson = "{\"objective\":{\"score\":null,\"status\":\"DEFERRED\",\"sampleCount\":0},\"teamwork\":{\"score\":null,\"status\":\"DEFERRED\",\"sampleCount\":0},\"exploration\":{\"score\":null,\"status\":\"DEFERRED\",\"sampleCount\":0},\"navigation\":{\"score\":null,\"status\":\"DEFERRED\",\"sampleCount\":0},\"toolUsage\":{\"score\":null,\"status\":\"DEFERRED\",\"sampleCount\":0},\"risk\":{\"score\":null,\"status\":\"DEFERRED\",\"sampleCount\":0},\"revive\":{\"score\":null,\"status\":\"DEFERRED\",\"sampleCount\":0}}";

    public async Task<AdaptiveSnapshotBuildResult> BuildPreMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var userIds = await db.MatchPlayerBindings.AsNoTracking()
            .Where(item => item.MatchId == matchId).Select(item => item.UserId)
            .OrderBy(item => item).ToArrayAsync(cancellationToken);
        var profiles = await db.PlayerAIProfiles.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId)).ToDictionaryAsync(item => item.UserId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rosterIdentity = ScenarioFingerprint.Hash(matchId.ToString("D"), string.Join(",", userIds.Select(x => x.ToString("D"))));
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        if (userIds.Length == 0) reasons.Add("ROSTER_EMPTY");
        var players = new List<AdaptiveInputSnapshotPlayer>(userIds.Length);
        foreach (var userId in userIds)
        {
            if (!profiles.TryGetValue(userId, out var profile))
            {
                reasons.Add("PROFILE_MISSING");
                players.Add(new AdaptiveInputSnapshotPlayer
                {
                    UserId = userId, ProfileAvailable = false, SurvivalStatus = "UNAVAILABLE",
                    NoiseStatus = "UNAVAILABLE", DeferredDimensionsJson = DeferredJson, CapturedAtUtc = now
                });
                continue;
            }
            if (!string.Equals(profile.ProfileFormulaVersion, SupportedProfileFormula, StringComparison.Ordinal))
                reasons.Add("PROFILE_VERSION_UNSUPPORTED");
            var survivalKey = ComparisonKey("SURVIVAL", profile, false);
            var noiseKey = ComparisonKey("NOISE", profile, true);
            players.Add(new AdaptiveInputSnapshotPlayer
            {
                UserId = userId, ProfileAvailable = true, ProfileLineageId = profile.ProfileLineageId,
                ProfileRevision = profile.ProfileRevision, ProfileFormulaVersion = profile.ProfileFormulaVersion,
                MatchScoreFormulaVersion = profile.MatchScoreFormulaVersion,
                NormalizationConfigVersion = profile.NormalizationConfigVersion,
                ProfileNoiseFilterVersion = profile.ProfileNoiseFilterVersion, AlphaConfigVersion = profile.AlphaConfigVersion,
                SurvivalScore = profile.SurvivalScore, SurvivalStatus = Status(profile.SurvivalStatus),
                SurvivalSampleCount = profile.SurvivalSampleCount, SurvivalComparisonKey = survivalKey,
                NoiseScore = profile.NoiseScore, NoiseStatus = Status(profile.NoiseStatus),
                NoiseSampleCount = profile.NoiseSampleCount, NoiseComparisonKey = noiseKey,
                DeferredDimensionsJson = DeferredJson, CapturedAtUtc = now
            });
            if (profile.SurvivalStatus != ProfileDimensionStatus.Active || profile.SurvivalSampleCount < 1
                || profile.NoiseStatus != ProfileDimensionStatus.Active || profile.NoiseSampleCount < 1)
                reasons.Add("INSUFFICIENT_OBSERVED_PROFILE_EVIDENCE");
        }
        var survival = Aggregate(players, true, reasons);
        var noise = Aggregate(players, false, reasons);
        var validity = reasons.Contains("ROSTER_EMPTY") || reasons.Contains("PROFILE_VERSION_UNSUPPORTED")
            || reasons.Contains("FORMULA_VERSION_CONFLICT") ? AdaptiveSnapshotValidity.Invalid
            : reasons.Count > 0 ? AdaptiveSnapshotValidity.Partial : AdaptiveSnapshotValidity.Valid;
        var snapshot = new AdaptiveInputSnapshot
        {
            SnapshotId = Guid.NewGuid(), MatchId = matchId, RosterIdentity = rosterIdentity,
            TeamSize = userIds.Length, Validity = validity,
            ReasonCodesJson = JsonSerializer.Serialize(reasons.OrderBy(x => x)),
            ProfileFormulaSemanticId = players.Where(x => x.ProfileAvailable).Select(x => x.ProfileFormulaVersion).Distinct().Count() == 1
                ? players.First(x => x.ProfileAvailable).ProfileFormulaVersion : null,
            SurvivalComparisonKey = survival.Key, NoiseComparisonKey = noise.Key,
            SurvivalAggregationStatus = survival.Status, NoiseAggregationStatus = noise.Status,
            SurvivalMeanObservedScore = survival.Mean, NoiseMeanObservedScore = noise.Mean,
            SurvivalObservedActiveCount = survival.Count, NoiseObservedActiveCount = noise.Count,
            CreatedAtUtc = now, Players = players
        };
        foreach (var player in players) player.SnapshotId = snapshot.SnapshotId;
        snapshot.SnapshotContentFingerprint = Fingerprint(snapshot);
        return new(snapshot, reasons.OrderBy(x => x).ToArray());
    }

    private static (string Status, string? Key, decimal? Mean, int Count) Aggregate(
        IReadOnlyList<AdaptiveInputSnapshotPlayer> players, bool survival, ISet<string> reasons)
    {
        var observed = players.Where(x => x.ProfileAvailable
            && (survival ? x.SurvivalStatus : x.NoiseStatus) == "ACTIVE"
            && (survival ? x.SurvivalSampleCount : x.NoiseSampleCount) > 0).ToArray();
        if (observed.Length == 0) return ("UNAVAILABLE", null, null, 0);
        var keys = observed.Select(x => survival ? x.SurvivalComparisonKey : x.NoiseComparisonKey).Distinct().ToArray();
        if (keys.Length != 1 || keys[0] is null) { reasons.Add("FORMULA_VERSION_CONFLICT"); return ("INVALID", null, null, observed.Length); }
        return ("AVAILABLE", keys[0], observed.Average(x => survival ? x.SurvivalScore!.Value : x.NoiseScore!.Value), observed.Length);
    }

    private static string ComparisonKey(string dimension, PlayerAIProfile p, bool noise) => ScenarioFingerprint.Hash(
        dimension, p.ProfileFormulaVersion, p.MatchScoreFormulaVersion,
        noise ? p.NormalizationConfigVersion : null, p.AlphaConfigVersion,
        noise ? p.ProfileNoiseFilterVersion : null);
    private static string Status(ProfileDimensionStatus status) => status switch
    { ProfileDimensionStatus.Active => "ACTIVE", ProfileDimensionStatus.ColdStart => "COLD_START", _ => "DEFERRED" };
    private static string Fingerprint(AdaptiveInputSnapshot s) => ScenarioFingerprint.Hash(
        s.MatchId.ToString("D"), s.DecisionPoint, s.RosterIdentity, s.TeamSize.ToString(CultureInfo.InvariantCulture),
        s.Validity.ToString().ToUpperInvariant(), s.ReasonCodesJson, s.ProfileFormulaSemanticId,
        s.SurvivalComparisonKey, s.NoiseComparisonKey,
        string.Join(";", s.Players.OrderBy(x => x.UserId).Select(x => string.Join(",",
            x.UserId.ToString("D"), x.ProfileRevision?.ToString(CultureInfo.InvariantCulture),
            x.SurvivalStatus, x.SurvivalScore?.ToString(CultureInfo.InvariantCulture), x.SurvivalSampleCount,
            x.NoiseStatus, x.NoiseScore?.ToString(CultureInfo.InvariantCulture), x.NoiseSampleCount))));
}
