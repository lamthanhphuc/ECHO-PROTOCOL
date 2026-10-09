using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.AED
{
    public static class AEDSnapshotMapper
    {
        private const string SupportedProfileFormula = "PROFILE_FORMULA_V1_1";

        public static bool TryMap(AEDSnapshotApiData dto,
            out AdaptiveInputSnapshot snapshot,
            out AdaptiveInputCurrencyValidation currency,
            out string reason)
        {
            snapshot = null;
            currency = null;
            reason = "AED_SNAPSHOT_MALFORMED";
            if (dto == null || !Guid.TryParse(dto.snapshotId, out var snapshotId)
                || !Guid.TryParse(dto.targetMatchId, out var matchId)
                || snapshotId == Guid.Empty || matchId == Guid.Empty
                || !DateTime.TryParse(dto.createdAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var created)
                || created.Kind != DateTimeKind.Utc
                || dto.teamSize < 1 || dto.players == null || dto.players.Length != dto.teamSize
                || string.IsNullOrWhiteSpace(dto.rosterIdentity)
                || string.IsNullOrWhiteSpace(dto.snapshotContentFingerprint)
                || !TryEnum(dto.decisionPoint, out ScenarioDecisionPoint point)
                || !TryEnum(dto.snapshotValidity, out SnapshotValidity validity)
                || !TryEnum(dto.survivalAggregationStatus, out RosterAggregationStatus survivalStatus)
                || !TryEnum(dto.noiseAggregationStatus, out RosterAggregationStatus noiseStatus)
                || !ScoreValid(dto.survivalMeanObservedScorePresent, dto.survivalMeanObservedScore)
                || !ScoreValid(dto.noiseMeanObservedScorePresent, dto.noiseMeanObservedScore))
                return false;

            var fingerprintVersion =
                string.IsNullOrWhiteSpace(dto.fingerprintVersion)
                    ? "V1"
                    : dto.fingerprintVersion;
            if (fingerprintVersion != "V1" && fingerprintVersion != "V2")
            {
                reason = "AED_FINGERPRINT_VERSION_UNSUPPORTED";
                return false;
            }

            var hasAvailableProfiles = dto.players.Any(p => p != null && p.profileAvailable);
            if (hasAvailableProfiles
                ? !string.Equals(dto.profileFormulaSemanticId, SupportedProfileFormula, StringComparison.Ordinal)
                : !string.IsNullOrWhiteSpace(dto.profileFormulaSemanticId))
            {
                reason = "AED_PROFILE_FORMULA_UNSUPPORTED";
                return false;
            }

            var profiles = new List<PlayerProfileSnapshot>(dto.teamSize);
            var revisions = new List<ProfileRevisionRef>(dto.teamSize);
            var missingProfileUserIds = new List<string>();
            var seen = new HashSet<Guid>();
            var survivalCold = 0;
            var noiseCold = 0;
            var survivalActive = 0;
            var noiseActive = 0;
            foreach (var player in dto.players)
            {
                if (player == null || !Guid.TryParse(player.userId, out var userId)
                    || userId == Guid.Empty || !seen.Add(userId)) return false;
                if (!player.profileAvailable)
                {
                    if (player.profileRevisionPresent
                        || !string.IsNullOrWhiteSpace(player.profileLineageId)
                        || player.survivalScorePresent || player.noiseScorePresent
                        || player.objectiveScorePresent || player.toolUsageScorePresent
                        || player.survivalSampleCount != 0 || player.noiseSampleCount != 0
                        || player.objectiveSampleCount != 0 || player.toolUsageSampleCount != 0
                        || player.survivalStatus != "UNAVAILABLE"
                        || player.noiseStatus != "UNAVAILABLE")
                    {
                        reason = "AED_MISSING_PROFILE_PAYLOAD_INVALID";
                        return false;
                    }
                    missingProfileUserIds.Add(userId.ToString("D"));
                    continue;
                }
                PlayerDimensionSnapshot objective = null;
                PlayerDimensionSnapshot toolUsage = null;
                if (!Guid.TryParse(player.profileLineageId, out var lineageId)
                    || lineageId == Guid.Empty || !player.profileRevisionPresent
                    || player.profileRevision < 0
                    || !TryDimension(player.survivalStatus, player.survivalScorePresent,
                        player.survivalScore, player.survivalSampleCount,
                        player.survivalComparisonKey, player.profileRevision,
                        out var survival)
                    || !TryDimension(player.noiseStatus, player.noiseScorePresent,
                        player.noiseScore, player.noiseSampleCount,
                        player.noiseComparisonKey, player.profileRevision,
                        out var noise)
                    || fingerprintVersion == "V2"
                    && (!TryOptionalDimension(player.objectiveStatus,
                            player.objectiveScorePresent, player.objectiveScore,
                            player.objectiveSampleCount, player.objectiveComparisonKey,
                            player.profileRevision, out objective)
                        || !TryOptionalDimension(player.toolUsageStatus,
                            player.toolUsageScorePresent, player.toolUsageScore,
                            player.toolUsageSampleCount, player.toolUsageComparisonKey,
                            player.profileRevision, out toolUsage))) return false;
                if (survival.Status == PlayerDimensionStatus.ColdStart) survivalCold++;
                if (noise.Status == PlayerDimensionStatus.ColdStart) noiseCold++;
                if (survival.Status == PlayerDimensionStatus.Active) survivalActive++;
                if (noise.Status == PlayerDimensionStatus.Active) noiseActive++;
                profiles.Add(new PlayerProfileSnapshot(userId.ToString("D"), player.profileRevision,
                    lineageId.ToString("D"), survival, noise,
                    fingerprintVersion == "V2" ? objective : null,
                    fingerprintVersion == "V2" ? toolUsage : null));
                revisions.Add(new ProfileRevisionRef(userId.ToString("D"), player.profileRevision));
            }

            if (missingProfileUserIds.Count > 0
                && (validity != SnapshotValidity.Partial
                    || dto.reasonCodes == null
                    || !dto.reasonCodes.Contains("PROFILE_MISSING")))
            {
                reason = "AED_COLD_START_VALIDITY_MISMATCH";
                return false;
            }
            if (dto.survivalObservedActiveCount != survivalActive
                || dto.noiseObservedActiveCount != noiseActive)
                return false;
            var survivalSummary = new RosterDimensionSummary(dto.survivalObservedActiveCount,
                survivalCold, missingProfileUserIds.Count, 0, (double)dto.survivalObservedActiveCount / dto.teamSize,
                survivalStatus, dto.survivalComparisonKey,
                dto.survivalMeanObservedScorePresent ? dto.survivalMeanObservedScore : null);
            var noiseSummary = new RosterDimensionSummary(dto.noiseObservedActiveCount,
                noiseCold, missingProfileUserIds.Count, 0, (double)dto.noiseObservedActiveCount / dto.teamSize,
                noiseStatus, dto.noiseComparisonKey,
                dto.noiseMeanObservedScorePresent ? dto.noiseMeanObservedScore : null);
            var objectiveSummary = OptionalSummary(profiles, true, dto.teamSize);
            var toolUsageSummary = OptionalSummary(profiles, false, dto.teamSize);
            if (fingerprintVersion == "V2")
            {
                if (!MatchesOptionalAggregate(
                        objectiveSummary,
                        dto.objectiveAggregationStatus,
                        dto.objectiveObservedActiveCount,
                        dto.objectiveComparisonKey,
                        dto.objectiveMeanObservedScorePresent,
                        dto.objectiveMeanObservedScore))
                {
                    reason = "AED_OBJECTIVE_AGGREGATE_MISMATCH";
                    return false;
                }

                if (!MatchesOptionalAggregate(
                        toolUsageSummary,
                        dto.toolUsageAggregationStatus,
                        dto.toolUsageObservedActiveCount,
                        dto.toolUsageComparisonKey,
                        dto.toolUsageMeanObservedScorePresent,
                        dto.toolUsageMeanObservedScore))
                {
                    reason = "AED_TOOL_USAGE_AGGREGATE_MISMATCH";
                    return false;
                }
            }
            if ((survivalActive > 0 && string.IsNullOrWhiteSpace(dto.survivalComparisonKey))
                || (noiseActive > 0 && string.IsNullOrWhiteSpace(dto.noiseComparisonKey)))
            {
                reason = "AED_COMPARISON_KEY_MISSING";
                return false;
            }
            var provenance = new AdaptiveInputProvenance(dto.profileFormulaSemanticId,
                dto.survivalComparisonKey, dto.noiseComparisonKey, string.Empty,
                revisions, string.Empty);
            snapshot = new AdaptiveInputSnapshot(snapshotId, dto.snapshotContentFingerprint,
                matchId, point, dto.phaseContext, created, dto.rosterIdentity, dto.teamSize,
                profiles, new RosterProfileSummary(dto.rosterIdentity, dto.teamSize,
                    survivalSummary, noiseSummary, objectiveSummary, toolUsageSummary),
                validity, dto.reasonCodes ?? Array.Empty<string>(),
                provenance, missingProfileUserIds);
            currency = new AdaptiveInputCurrencyValidation(dto.targetMatchCurrent,
                dto.decisionPointCurrent, dto.phaseContextCurrent,
                dto.rosterCurrent, dto.profileRevisionsCurrent, dto.snapshotFingerprintValid,
                dto.profileSemanticsSupported);
            reason = string.Empty;
            return true;
        }

        private static RosterDimensionSummary OptionalSummary(
            IReadOnlyList<PlayerProfileSnapshot> profiles,
            bool objective,
            int teamSize)
        {
            var dimensions = profiles
                .Select(p => objective ? p.Objective : p.ToolUsage)
                .ToArray();
            var active = dimensions
                .Where(d => d != null
                    && d.Status == PlayerDimensionStatus.Active
                    && d.SampleCount > 0
                    && d.Score.HasValue)
                .ToArray();
            var keys = active
                .Select(d => d.ComparisonSemanticKey)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var status = active.Length == 0
                ? RosterAggregationStatus.Unavailable
                : keys.Length == 1 && !string.IsNullOrWhiteSpace(keys[0])
                    ? RosterAggregationStatus.Available
                    : RosterAggregationStatus.Invalid;

            return new RosterDimensionSummary(
                active.Length,
                dimensions.Count(d => d != null
                    && d.Status == PlayerDimensionStatus.ColdStart),
                teamSize - profiles.Count + dimensions.Count(d => d == null),
                dimensions.Count(d => d != null
                    && d.Status == PlayerDimensionStatus.Deferred),
                (double)active.Length / teamSize,
                status,
                status == RosterAggregationStatus.Available ? keys[0] : string.Empty,
                status == RosterAggregationStatus.Available
                    ? active.Average(d => d.Score.Value)
                    : (double?)null);
        }

        private static bool MatchesOptionalAggregate(
            RosterDimensionSummary actual,
            string rawStatus,
            int reportedCount,
            string reportedKey,
            bool meanPresent,
            double reportedMean)
        {
            if (!TryEnum(rawStatus, out RosterAggregationStatus expectedStatus))
                return false;
            if (actual.AggregationStatus != expectedStatus
                || actual.ObservedActiveCount != reportedCount)
                return false;
            if (!string.Equals(actual.ComparisonSemanticKey,
                    reportedKey ?? string.Empty, StringComparison.Ordinal))
                return false;
            if (actual.MeanObservedScore.HasValue != meanPresent)
                return false;
            return !meanPresent || Math.Abs(actual.MeanObservedScore.Value - reportedMean) <= 0.000001;
        }

        private static bool TryOptionalDimension(string raw, bool hasScore, double score,
            int sampleCount, string key, long revision, out PlayerDimensionSnapshot dimension)
        {
            dimension = null;
            if (!TryEnum(raw, out PlayerDimensionStatus status)
                || !ScoreValid(hasScore, score) || sampleCount < 0)
                return false;
            if (status == PlayerDimensionStatus.Active
                && (!hasScore || sampleCount == 0 || string.IsNullOrWhiteSpace(key)))
                return false;
            dimension = new PlayerDimensionSnapshot(hasScore ? score : null,
                status, sampleCount, key, revision);
            return true;
        }

        private static bool TryDimension(string raw, bool hasScore, double score,
            int sampleCount, string key, long revision, out PlayerDimensionSnapshot dimension)
        {
            dimension = null;
            if (!TryEnum(raw, out PlayerDimensionStatus status)
                || !ScoreValid(hasScore, score) || sampleCount < 0
                || string.IsNullOrWhiteSpace(key)) return false;
            if (status == PlayerDimensionStatus.Active && (!hasScore || sampleCount == 0))
                return false;
            dimension = new PlayerDimensionSnapshot(hasScore ? score : null,
                status, sampleCount, key, revision);
            return true;
        }

        private static bool ScoreValid(bool present, double score) =>
            !present || (!double.IsNaN(score) && !double.IsInfinity(score)
                && score >= 0 && score <= 100);

        private static bool TryEnum<T>(string raw, out T value) where T : struct
        {
            value = default;
            return !string.IsNullOrWhiteSpace(raw)
                && Enum.TryParse(raw.Replace("_", string.Empty), true, out value)
                && Enum.IsDefined(typeof(T), value);
        }
    }
}
