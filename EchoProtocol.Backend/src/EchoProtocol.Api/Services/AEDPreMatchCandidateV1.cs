using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services;

public static class AEDPreMatchCandidateV1
{
    private const string SurvivalRelief =
        "AED-PRE-010-SURVIVAL-LOW-RELIEVE";
    private const string NoiseRelief =
        "AED-PRE-020-NOISE-LOW-RELIEVE";
    private const string Increase =
        "AED-PRE-030-BOTH-HIGH-INCREASE";
    private const string Hold =
        "AED-PRE-040-MIXED-HOLD";

    public static bool TrySelect(
        string ruleId,
        ScenarioConfigDefinition baseline,
        IReadOnlyList<ScenarioConfigDefinition> approvedCandidates,
        IReadOnlyList<double> registeredValues,
        out ScenarioConfigDefinition? selected,
        out string reason)
    {
        selected = null;

        if (baseline is null ||
            approvedCandidates is null ||
            baseline.ConfigSource != ScenarioConfigSource.Fixed ||
            !baseline.IsFixedFallback ||
            !baseline.IsActive ||
            !baseline.IsProductionApproved ||
            !double.IsFinite(baseline.ChaseSpeed))
        {
            reason = "BASELINE_INVALID";
            return false;
        }

        if (ruleId == Hold)
        {
            reason = "HOLD_RULE";
            return false;
        }

        var relief = ruleId is SurvivalRelief or NoiseRelief;

        if (!relief && ruleId != Increase)
        {
            reason = "RULE_UNSUPPORTED";
            return false;
        }

        if (!relief &&
            !string.Equals(
                baseline.MonsterType,
                "STALKER",
                StringComparison.OrdinalIgnoreCase))
        {
            reason = "KEY_NOT_APPLICABLE";
            return false;
        }

        if (registeredValues == null ||
            registeredValues.Count == 0 ||
            registeredValues.Any(x =>
                !double.IsFinite(x) ||
                (relief && (x < 0 || x != Math.Truncate(x)))) ||
            registeredValues.Distinct().Count() != registeredValues.Count)
        {
            reason = "PARAMETER_REGISTRY_INVALID";
            return false;
        }

        var current = relief
            ? baseline.SupportItemBudget
            : baseline.ChaseSpeed;

        if (!registeredValues.Contains(current))
        {
            reason = "PARAMETER_REGISTRY_INVALID";
            return false;
        }

        var higher = registeredValues
            .Where(x => x > current)
            .OrderBy(x => x)
            .ToArray();

        if (higher.Length == 0)
        {
            reason = "VALUE_LIMIT_REACHED";
            return false;
        }

        var expectedNext = higher[0];

        var candidates = approvedCandidates
            .Where(x =>
                x is not null &&
                IsCompatible(x, baseline) &&
                (relief
                    ? x.SupportItemBudget == expectedNext &&
                      x.ChaseSpeed == baseline.ChaseSpeed
                    : x.ChaseSpeed == expectedNext &&
                      x.SupportItemBudget == baseline.SupportItemBudget))
            .ToArray();

        if (candidates.Length == 0)
        {
            reason = "NO_APPROVED_NEXT_VALUE";
            return false;
        }

        if (candidates.Length != 1)
        {
            reason = "AMBIGUOUS_CANDIDATE";
            return false;
        }

        selected = candidates[0];
        reason = string.Empty;
        return true;
    }

    private static bool IsCompatible(
        ScenarioConfigDefinition candidate,
        ScenarioConfigDefinition baseline)
    {
        return candidate.ConfigSource == ScenarioConfigSource.Adaptive &&
               candidate.IsActive &&
               candidate.IsProductionApproved &&
               !candidate.IsFixedFallback &&
               double.IsFinite(candidate.ChaseSpeed) &&
               candidate.SchemaVersion == baseline.SchemaVersion &&
               candidate.PolicyVersion == baseline.PolicyVersion &&
               candidate.UnityCompatibilityVersion ==
                   baseline.UnityCompatibilityVersion &&
               candidate.ContentWhitelistVersion ==
                   baseline.ContentWhitelistVersion &&
               candidate.FallbackConfigId ==
                   baseline.ScenarioConfigId &&
               candidate.FallbackConfigVersion ==
                   baseline.ScenarioConfigVersion &&
               candidate.MapId == baseline.MapId &&
               candidate.MonsterType == baseline.MonsterType &&
               candidate.ObjectiveSpawnSetId ==
                   baseline.ObjectiveSpawnSetId &&
               candidate.DetectionFillRate ==
                   baseline.DetectionFillRate &&
               candidate.DetectionDecayRate ==
                   baseline.DetectionDecayRate &&
               candidate.SearchDuration == baseline.SearchDuration &&
               candidate.RouteModifier == baseline.RouteModifier &&
               candidate.EscapeDoorTimerSeconds ==
                   baseline.EscapeDoorTimerSeconds;
    }
}
