using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed record TeamMatchFacts(
    Guid MatchId,
    MatchOutcome Outcome,
    int SurvivorCount,
    bool IsRelationallyFinal);

public static class TeamProfileProjector
{
    private const string SchemaVersion = "1.1";

    public static TeamProfileProjection Project(
        TeamMatchFacts facts,
        IReadOnlyCollection<TelemetryEventDocument> source,
        IReadOnlySet<Guid> boundUsers,
        ITeamProfilePolicy policy)
    {
        var deduplicated = Deduplicate(source, out var identityConflict)
            .OrderBy(item => item.EventSequence)
            .ThenBy(item => item.Id)
            .ToArray();
        var sourceFingerprint = Fingerprint(deduplicated.Select(item => item.SemanticFingerprint));

        if (!facts.IsRelationallyFinal)
        {
            return FinalizeProjection(facts.MatchId, TeamProfileProcessingStatus.Pending,
                "MATCH_NOT_FINALIZED", TelemetryCompleteness.Unknown, null,
                TeamMetricStatus.Unavailable, null, TeamMetricStatus.Unavailable,
                null, TeamMetricStatus.Unavailable, sourceFingerprint, policy);
        }

        if (identityConflict || deduplicated.Any(item => item.MatchId != facts.MatchId) ||
            deduplicated.Any(item => item.SchemaVersion != SchemaVersion) ||
            deduplicated.Any(item => item.UserId.HasValue && !boundUsers.Contains(item.UserId.Value)) ||
            deduplicated.Any(item => item.EventSequence <= 0) ||
            deduplicated.GroupBy(item => item.EventSequence).Any(group => group.Count() > 1) ||
            deduplicated.Any(item => item.SemanticFingerprint.Length != 64 ||
                                     !item.SemanticFingerprint.All(Uri.IsHexDigit)))
        {
            return FinalizeProjection(facts.MatchId, TeamProfileProcessingStatus.Invalid,
                "TELEMETRY_INTEGRITY_INVALID", TelemetryCompleteness.Invalid, null,
                TeamMetricStatus.Invalid, null, TeamMetricStatus.Invalid,
                null, TeamMetricStatus.Invalid, sourceFingerprint, policy);
        }

        var starts = deduplicated.Where(item => item.EventType == "MATCH_STARTED").ToArray();
        var ends = deduplicated.Where(item => item.EventType == "MATCH_ENDED").ToArray();
        if (starts.Length != 1 || starts[0].EventSequence != 1 || ends.Length != 1)
        {
            return FinalizeProjection(facts.MatchId, TeamProfileProcessingStatus.Ineligible,
                starts.Length != 1 ? "MISSING_MATCH_START" : "MISSING_MATCH_END",
                TelemetryCompleteness.Incomplete, null, TeamMetricStatus.Unavailable,
                null, TeamMetricStatus.Unavailable, null, TeamMetricStatus.Unavailable,
                sourceFingerprint, policy);
        }

        var terminal = ends[0];
        if (terminal.ReasonCode == "MATCH_ABORTED")
        {
            return FinalizeProjection(facts.MatchId, TeamProfileProcessingStatus.Ineligible,
                "MATCH_ABORTED", TelemetryCompleteness.Complete, null,
                TeamMetricStatus.Unavailable, null, TeamMetricStatus.Unavailable,
                null, TeamMetricStatus.Unavailable, sourceFingerprint, policy);
        }

        if (!TryReadInt32(starts[0].ValueJson, "context", "teamSize", out var teamSize) ||
            teamSize <= 0 ||
            (boundUsers.Count > 0 && teamSize != boundUsers.Count) ||
            !TryReadInt32(terminal.ValueJson, "data", "survivorCount", out var survivorCount) ||
            survivorCount < 0 || survivorCount > teamSize || survivorCount != facts.SurvivorCount ||
            !TryReadString(terminal.ValueJson, "data", "outcome", out var outcome) ||
            outcome != (facts.Outcome == MatchOutcome.WIN ? "SUCCESS" : "FAILURE") ||
            terminal.ReasonCode != (facts.Outcome == MatchOutcome.WIN ? "TEAM_ESCAPED" : "TEAM_ELIMINATED"))
        {
            return FinalizeProjection(facts.MatchId, TeamProfileProcessingStatus.Invalid,
                "MATCH_TERMINAL_PROVENANCE_INVALID", TelemetryCompleteness.Invalid,
                null, TeamMetricStatus.Invalid, null, TeamMetricStatus.Invalid,
                null, TeamMetricStatus.Invalid, sourceFingerprint, policy);
        }

        var complete = terminal.EventSequence == deduplicated.Length &&
            deduplicated.Select(item => item.EventSequence).SequenceEqual(
                Enumerable.Range(1, deduplicated.Length).Select(item => (long)item));
        var completeness = complete ? TelemetryCompleteness.Complete : TelemetryCompleteness.Incomplete;
        var survival = 100m * Math.Clamp((decimal)survivorCount / teamSize, 0m, 1m);

        decimal? objectiveTime = null;
        var objectiveStatus = TeamMetricStatus.Unavailable;
        var objectiveReason = complete ? string.Empty : "STREAM_INCOMPLETE";
        if (complete)
        {
            (objectiveTime, objectiveStatus, objectiveReason) = CalculateObjectiveTime(deduplicated, policy);
        }

        decimal? objectiveSpeed = null;
        var objectiveSpeedStatus = objectiveStatus switch
        {
            TeamMetricStatus.Invalid => TeamMetricStatus.Invalid,
            TeamMetricStatus.Unavailable => TeamMetricStatus.Unavailable,
            _ => TeamMetricStatus.Invalid
        };
        if (objectiveStatus == TeamMetricStatus.Available && objectiveTime.HasValue)
        {
            if (policy.TryValidateObjectiveNormalization(out _))
            {
                var normalized = Math.Clamp(
                    (objectiveTime.Value - policy.ObjectiveTimeMin!.Value) /
                    (policy.ObjectiveTimeMax!.Value - policy.ObjectiveTimeMin.Value), 0m, 1m);
                objectiveSpeed = 100m * (1m - normalized);
                objectiveSpeedStatus = TeamMetricStatus.Available;
            }
        }

        var reason = objectiveStatus == TeamMetricStatus.Available
            ? (objectiveSpeedStatus == TeamMetricStatus.Available
                ? "REQUIRED_COMPONENT_DEFERRED"
                : "OBJECTIVE_NORMALIZATION_NOT_CONFIGURED")
            : objectiveReason;
        return FinalizeProjection(facts.MatchId, TeamProfileProcessingStatus.Processed,
            reason, completeness, objectiveTime, objectiveStatus, objectiveSpeed,
            objectiveSpeedStatus, survival, TeamMetricStatus.Available,
            sourceFingerprint, policy);
    }

    private static (decimal? Value, TeamMetricStatus Status, string Reason) CalculateObjectiveTime(
        IReadOnlyList<TelemetryEventDocument> events,
        ITeamProfilePolicy policy)
    {
        if (!policy.TryValidatePhaseRegistry(out _))
        {
            return (null, TeamMetricStatus.Invalid, "PHASE_REGISTRY_NOT_CONFIGURED");
        }

        var intervals = new List<PhaseInterval>();
        foreach (var phase in policy.ObjectiveBearingPhases.Order(StringComparer.Ordinal))
        {
            var starts = events.Where(item => item.EventType == "PHASE_STARTED" && HasPhase(item, phase)).ToArray();
            var completions = events.Where(item => item.EventType == "PHASE_COMPLETED" && HasPhase(item, phase)).ToArray();
            if (starts.Length == 0 || completions.Length == 0)
            {
                return (null, TeamMetricStatus.Unavailable, "OBJECTIVE_PHASE_PAIR_MISSING");
            }

            if (starts.Length != 1 || completions.Length != 1 ||
                starts[0].EventSequence >= completions[0].EventSequence)
            {
                return (null, TeamMetricStatus.Invalid, "OBJECTIVE_PHASE_SEQUENCE_INVALID");
            }

            if (completions[0].Ts < starts[0].Ts)
            {
                return (null, TeamMetricStatus.Invalid, "OBJECTIVE_PHASE_DURATION_NEGATIVE");
            }

            intervals.Add(new PhaseInterval(phase, starts[0].Ts, completions[0].Ts));
        }

        for (var first = 0; first < intervals.Count; first++)
        {
            for (var second = first + 1; second < intervals.Count; second++)
            {
                var left = intervals[first];
                var right = intervals[second];
                if (left.Start < right.End && right.Start < left.End &&
                    !policy.IsOverlapAllowed(left.Phase, right.Phase))
                {
                    return (null, TeamMetricStatus.Invalid, "OBJECTIVE_PHASE_OVERLAP_FORBIDDEN");
                }
            }
        }

        var ticks = intervals.Sum(item => item.End.Ticks - item.Start.Ticks);
        return ((decimal)ticks / TimeSpan.TicksPerSecond, TeamMetricStatus.Available, "AVAILABLE");
    }

    private static IReadOnlyList<TelemetryEventDocument> Deduplicate(
        IReadOnlyCollection<TelemetryEventDocument> source,
        out bool conflict)
    {
        conflict = false;
        var result = new List<TelemetryEventDocument>();
        foreach (var group in source.GroupBy(item => item.Id))
        {
            var first = group.First();
            if (group.Any(item => item.SemanticFingerprint != first.SemanticFingerprint))
            {
                conflict = true;
            }
            result.Add(first);
        }
        return result;
    }

    private static bool HasPhase(TelemetryEventDocument document, string phase) =>
        TryReadString(document.ValueJson, "context", "phase", out var actual) && actual == phase;

    private static bool TryReadString(BsonDocument root, string parent, string name, out string value)
    {
        value = string.Empty;
        return root.TryGetValue(parent, out var parentValue) && parentValue.IsBsonDocument &&
               parentValue.AsBsonDocument.TryGetValue(name, out var field) && field.IsString &&
               !string.IsNullOrWhiteSpace(value = field.AsString);
    }

    private static bool TryReadInt32(BsonDocument root, string parent, string name, out int value)
    {
        value = 0;
        return root.TryGetValue(parent, out var parentValue) && parentValue.IsBsonDocument &&
               parentValue.AsBsonDocument.TryGetValue(name, out var field) && field.IsInt32 &&
               (value = field.AsInt32) >= 0;
    }

    private static TeamProfileProjection FinalizeProjection(
        Guid matchId,
        TeamProfileProcessingStatus processingStatus,
        string reason,
        TelemetryCompleteness completeness,
        decimal? objectiveTime,
        TeamMetricStatus objectiveTimeStatus,
        decimal? objectiveSpeed,
        TeamMetricStatus objectiveSpeedStatus,
        decimal? survival,
        TeamMetricStatus survivalStatus,
        string sourceFingerprint,
        ITeamProfilePolicy policy)
    {
        var canonical = string.Join('\n', matchId.ToString("D"), processingStatus, reason,
            completeness, Format(objectiveTime), objectiveTimeStatus, Format(objectiveSpeed),
            objectiveSpeedStatus, Format(survival), survivalStatus,
            policy.ProfileFormulaVersion, policy.TeamPerformanceFormulaVersion ?? "null",
            policy.PhaseRegistryVersion ?? "null", policy.NormalizationConfigVersion ?? "null",
            sourceFingerprint);
        var projectionFingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new TeamProfileProjection(matchId, processingStatus, reason, completeness,
            objectiveTime, objectiveTimeStatus, objectiveSpeed, objectiveSpeedStatus,
            survival, survivalStatus, TeamPerformanceStatus.Incomplete, null,
            SchemaVersion, sourceFingerprint, projectionFingerprint);
    }

    private static string Format(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "null";

    private sealed record PhaseInterval(string Phase, DateTime Start, DateTime End);

    private static string Fingerprint(IEnumerable<string> parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
}
