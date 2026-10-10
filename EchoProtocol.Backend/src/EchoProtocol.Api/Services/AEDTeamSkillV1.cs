using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.Api.Services;

public sealed record AEDTeamMemberSkillV1(
    Guid UserId,
    decimal? Score,
    bool ConfidenceComplete);

public sealed record AEDTeamSkillSnapshotV1(
    Guid MatchId,
    int RosterSize,
    IReadOnlyList<AEDTeamMemberSkillV1> Members,
    decimal? Mean,
    decimal? Weakest,
    decimal? Variance,
    bool HasUncertainPlayer,
    bool ConfidenceComplete);

public static class AEDTeamSkillProjectorV1
{
    public const string Version = "AED_TEAM_SKILL_V1";

    public static AEDTeamSkillSnapshotV1 Project(
        Guid matchId,
        IEnumerable<Guid> roster,
        IEnumerable<AEDHistoricalSkillDimensionV1> dimensions)
    {
        if (matchId == Guid.Empty || roster == null || dimensions == null)
            throw new ArgumentException("INVALID_TEAM_SKILL_INPUT");

        var users = roster.Distinct().OrderBy(x => x).ToArray();
        if (users.Length == 0 || users.Any(x => x == Guid.Empty))
            throw new ArgumentException("INVALID_TEAM_ROSTER");

        var source = dimensions.ToArray();

        var required = new[]
        {
            AEDSkillDimensionV1.Survival,
            AEDSkillDimensionV1.Evasion,
            AEDSkillDimensionV1.Objective
        };

        var members = new List<AEDTeamMemberSkillV1>();

        foreach (var userId in users)
        {
            var rows = source.Where(x =>
                x.UserId == userId &&
                required.Contains(x.Dimension)).ToArray();

            var contextComplete = rows
                .Select(x => x.ComparisonContextKey)
                .Distinct(StringComparer.Ordinal)
                .Count() == 1 &&
                !string.IsNullOrWhiteSpace(rows.FirstOrDefault()?.ComparisonContextKey);

            var complete = contextComplete && required.All(d =>
                rows.Count(x =>
                    x.Dimension == d &&
                    x.Confidence == AEDSkillConfidenceV1.Sufficient &&
                    x.ReadyForIncrease &&
                    x.Score.HasValue) == 1);

            decimal? score = complete
                ? rows.Average(x => x.Score!.Value)
                : null;

            members.Add(new AEDTeamMemberSkillV1(
                userId, score, complete));
        }

        var allComplete = members.All(x => x.ConfidenceComplete);

        decimal? mean = allComplete
            ? members.Average(x => x.Score!.Value)
            : null;

        decimal? weakest = allComplete
            ? members.Min(x => x.Score!.Value)
            : null;

        decimal? variance = allComplete
            ? members.Average(x =>
                (x.Score!.Value - mean!.Value) *
                (x.Score.Value - mean.Value))
            : null;

        return new AEDTeamSkillSnapshotV1(
            matchId,
            users.Length,
            members.AsReadOnly(),
            mean,
            weakest,
            variance,
            !allComplete,
            allComplete);
    }
}
