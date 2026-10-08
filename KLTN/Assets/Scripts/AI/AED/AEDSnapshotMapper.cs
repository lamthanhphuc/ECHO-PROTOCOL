using System;
using System.Collections.Generic;
using System.Globalization;
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

            if (!string.Equals(dto.profileFormulaSemanticId, SupportedProfileFormula, StringComparison.Ordinal))
            {
                reason = "AED_PROFILE_FORMULA_UNSUPPORTED";
                return false;
            }

            var profiles = new List<PlayerProfileSnapshot>(dto.teamSize);
            var revisions = new List<ProfileRevisionRef>(dto.teamSize);
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
                    reason = "AED_PROFILE_MISSING";
                    return false;
                }
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
                        out var noise)) return false;
                if (survival.Status == PlayerDimensionStatus.ColdStart) survivalCold++;
                if (noise.Status == PlayerDimensionStatus.ColdStart) noiseCold++;
                if (survival.Status == PlayerDimensionStatus.Active) survivalActive++;
                if (noise.Status == PlayerDimensionStatus.Active) noiseActive++;
                profiles.Add(new PlayerProfileSnapshot(userId.ToString("D"), player.profileRevision,
                    lineageId.ToString("D"), survival, noise));
                revisions.Add(new ProfileRevisionRef(userId.ToString("D"), player.profileRevision));
            }

            if (dto.survivalObservedActiveCount != survivalActive
                || dto.noiseObservedActiveCount != noiseActive)
                return false;
            var survivalSummary = new RosterDimensionSummary(dto.survivalObservedActiveCount,
                survivalCold, 0, 0, (double)dto.survivalObservedActiveCount / dto.teamSize,
                survivalStatus, dto.survivalComparisonKey,
                dto.survivalMeanObservedScorePresent ? dto.survivalMeanObservedScore : null);
            var noiseSummary = new RosterDimensionSummary(dto.noiseObservedActiveCount,
                noiseCold, 0, 0, (double)dto.noiseObservedActiveCount / dto.teamSize,
                noiseStatus, dto.noiseComparisonKey,
                dto.noiseMeanObservedScorePresent ? dto.noiseMeanObservedScore : null);
            if (string.IsNullOrWhiteSpace(dto.survivalComparisonKey)
                || string.IsNullOrWhiteSpace(dto.noiseComparisonKey))
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
                    survivalSummary, noiseSummary), validity, dto.reasonCodes ?? Array.Empty<string>(),
                provenance);
            currency = new AdaptiveInputCurrencyValidation(dto.targetMatchCurrent,
                dto.decisionPointCurrent, dto.phaseContextCurrent,
                dto.rosterCurrent, dto.profileRevisionsCurrent, dto.snapshotFingerprintValid,
                dto.profileSemanticsSupported);
            reason = string.Empty;
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
