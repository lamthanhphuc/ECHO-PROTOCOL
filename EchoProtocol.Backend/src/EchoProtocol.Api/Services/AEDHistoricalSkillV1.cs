using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.Api.Services;

public enum AEDSkillDimensionV1
{
    Survival,
    Evasion,
    Objective,
    MinionCounterplay,
    ToolEffectiveness,
    ResourceManagement,
    Teamwork,
    RiskStyle
}

public enum AEDSkillConfidenceV1
{
    ColdStart,
    Insufficient,
    Sufficient,
    Invalid
}

public sealed record AEDVerifiedSkillObservationV1(
    Guid MatchId,
    Guid UserId,
    AEDSkillDimensionV1 Dimension,
    string ComparisonContextKey,
    string SourceFingerprint,
    string MetricVersion,
    decimal? NormalizedValue,
    int Eligible,
    int Resolved,
    int Censored,
    bool BackendVerified,
    bool SourceComplete);

public sealed record AEDHistoricalSkillDimensionV1(
    Guid UserId,
    AEDSkillDimensionV1 Dimension,
    string ComparisonContextKey,
    decimal? Score,
    int DistinctMatches,
    int ResolvedOpportunities,
    int CensoredOpportunities,
    AEDSkillConfidenceV1 Confidence,
    bool ReadyForIncrease);

public static class AEDHistoricalSkillProjectorV1
{
    public const string Version = "AED_HISTORICAL_SKILL_V1";
    public const int MinimumDistinctMatches = 3;
    public const int MinimumResolvedOpportunities = 8;

    public static IReadOnlyList<AEDHistoricalSkillDimensionV1> Project(
        Guid userId,
        string contextKey,
        IEnumerable<AEDVerifiedSkillObservationV1> observations)
    {
        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(contextKey) ||
            observations == null)
            throw new ArgumentException("INVALID_SKILL_PROJECTION_INPUT");

        var source = observations
            .Where(x => x.UserId == userId &&
                        x.ComparisonContextKey == contextKey)
            .ToArray();

        var results = new List<AEDHistoricalSkillDimensionV1>();

        foreach (AEDSkillDimensionV1 dimension in
                 Enum.GetValues(typeof(AEDSkillDimensionV1)))
        {
            var rows = source.Where(x => x.Dimension == dimension)
                .ToArray();

            if (rows.Length == 0)
            {
                results.Add(new(userId, dimension, contextKey,
                    null, 0, 0, 0,
                    AEDSkillConfidenceV1.ColdStart, false));
                continue;
            }

            var invalid = rows.Any(x =>
                x.MatchId == Guid.Empty ||
                !x.BackendVerified ||
                !x.SourceComplete ||
                string.IsNullOrWhiteSpace(x.SourceFingerprint) ||
                x.MetricVersion != "AED_METRIC_OPPORTUNITY_V1" ||
                x.Eligible < 0 ||
                x.Resolved < 0 ||
                x.Censored < 0 ||
                x.Resolved + x.Censored > x.Eligible ||
                (x.Resolved > 0 &&
                    (!x.NormalizedValue.HasValue ||
                     x.NormalizedValue < 0m ||
                     x.NormalizedValue > 1m)) ||
                (x.Resolved == 0 && x.NormalizedValue.HasValue));

            var duplicateConflicts = rows
                .GroupBy(x => new { x.MatchId, x.SourceFingerprint })
                .Any(group => group.Distinct().Count() > 1);

            if (invalid || duplicateConflicts)
            {
                results.Add(new(userId, dimension, contextKey,
                    null, 0, 0, 0,
                    AEDSkillConfidenceV1.Invalid, false));
                continue;
            }

            var unique = rows
                .GroupBy(x => new { x.MatchId, x.SourceFingerprint })
                .Select(g => g.First())
                .ToArray();

            var resolved = unique.Where(x => x.Resolved > 0)
                .GroupBy(x => x.MatchId)
                .Select(group => new
                {
                    MatchId = group.Key,
                    Count = group.Sum(x => x.Resolved),
                    Score = group.Sum(x =>
                        x.NormalizedValue!.Value * x.Resolved)
                        / group.Sum(x => x.Resolved)
                })
                .ToArray();

            var matchCount = resolved.Length;
            var resolvedCount = unique.Sum(x => x.Resolved);
            var censoredCount = unique.Sum(x => x.Censored);
            var eligibleCount = unique.Sum(x => x.Eligible);

            decimal? score = matchCount == 0
                ? null
                : resolved.Average(x => x.Score);

            var sufficient =
                matchCount >= MinimumDistinctMatches &&
                resolvedCount >= MinimumResolvedOpportunities &&
                eligibleCount > 0 &&
                censoredCount * 4 <= eligibleCount;

            var confidence = matchCount == 0
                ? AEDSkillConfidenceV1.ColdStart
                : sufficient
                    ? AEDSkillConfidenceV1.Sufficient
                    : AEDSkillConfidenceV1.Insufficient;

            results.Add(new(
                userId,
                dimension,
                contextKey,
                score,
                matchCount,
                resolvedCount,
                censoredCount,
                confidence,
                sufficient &&
                    dimension != AEDSkillDimensionV1.RiskStyle));
        }

        return results.AsReadOnly();
    }
}
