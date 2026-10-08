using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Entities;

namespace EchoProtocol.Api.Services;

public static class ScenarioFingerprint
{
    public static string Hash(params string?[] parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("|", parts.Select(item => item ?? "<null>"))))).ToLowerInvariant();

    public static string Config(ScenarioConfigDefinition item) => Hash(
        item.ScenarioConfigId, item.ScenarioConfigVersion, item.SchemaVersion, item.PolicyVersion,
        item.ConfigSource.ToString().ToUpperInvariant(), item.MapId, item.MonsterType,
        item.ObjectiveSpawnSetId, item.SupportItemBudget.ToString(CultureInfo.InvariantCulture),
        item.DetectionFillRate.ToString("R", CultureInfo.InvariantCulture),
        item.DetectionDecayRate.ToString("R", CultureInfo.InvariantCulture),
        item.ChaseSpeed.ToString("R", CultureInfo.InvariantCulture),
        item.SearchDuration.ToString("R", CultureInfo.InvariantCulture), item.RouteModifier,
        item.EscapeDoorTimerSeconds.ToString("R", CultureInfo.InvariantCulture),
        item.FallbackConfigId, item.FallbackConfigVersion, item.ContentWhitelistVersion,
        item.UnityCompatibilityVersion);

    public static string Snapshot(AdaptiveInputSnapshot s) => s.FingerprintVersion == "V2"
        ? Hash(
            s.FingerprintVersion, s.MatchId.ToString("D"), s.DecisionPoint, s.RosterIdentity,
            s.TeamSize.ToString(CultureInfo.InvariantCulture),
            s.Validity.ToString().ToUpperInvariant(), s.ReasonCodesJson,
            s.ProfileFormulaSemanticId, s.SurvivalComparisonKey, s.NoiseComparisonKey,
            s.ObjectiveAggregationStatus, s.ObjectiveComparisonKey,
            s.ObjectiveMeanObservedScore?.ToString(CultureInfo.InvariantCulture),
            s.ObjectiveObservedActiveCount.ToString(CultureInfo.InvariantCulture),
            s.ToolUsageAggregationStatus, s.ToolUsageComparisonKey,
            s.ToolUsageMeanObservedScore?.ToString(CultureInfo.InvariantCulture),
            s.ToolUsageObservedActiveCount.ToString(CultureInfo.InvariantCulture),
            string.Join(";", s.Players.OrderBy(x => x.UserId).Select(x => string.Join(",",
                x.UserId.ToString("D"), x.ProfileRevision?.ToString(CultureInfo.InvariantCulture),
                x.SurvivalStatus, x.SurvivalScore?.ToString(CultureInfo.InvariantCulture), x.SurvivalSampleCount,
                x.NoiseStatus, x.NoiseScore?.ToString(CultureInfo.InvariantCulture), x.NoiseSampleCount,
                x.DeferredDimensionsJson))))
        : Hash(
            s.MatchId.ToString("D"), s.DecisionPoint, s.RosterIdentity,
            s.TeamSize.ToString(CultureInfo.InvariantCulture),
            s.Validity.ToString().ToUpperInvariant(), s.ReasonCodesJson,
            s.ProfileFormulaSemanticId, s.SurvivalComparisonKey, s.NoiseComparisonKey,
            string.Join(";", s.Players.OrderBy(x => x.UserId).Select(x => string.Join(",",
                x.UserId.ToString("D"), x.ProfileRevision?.ToString(CultureInfo.InvariantCulture),
                x.SurvivalStatus, x.SurvivalScore?.ToString(CultureInfo.InvariantCulture), x.SurvivalSampleCount,
                x.NoiseStatus, x.NoiseScore?.ToString(CultureInfo.InvariantCulture), x.NoiseSampleCount))));
}
