using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.AED;

namespace EchoProtocol.AI.Common.Profile
{
    public enum SnapshotValidity { Valid, Partial, Invalid }
    public enum PlayerDimensionStatus { ColdStart, Active, Deferred }
    public enum RosterAggregationStatus { Available, Unavailable, Invalid }

    public sealed class PlayerDimensionSnapshot
    {
        public PlayerDimensionSnapshot(
            double? score,
            PlayerDimensionStatus status,
            int sampleCount,
            string comparisonSemanticKey,
            long? lastUpdatedRevision)
        {
            if (score.HasValue && (double.IsNaN(score.Value) || double.IsInfinity(score.Value) || score.Value < 0d || score.Value > 100d))
            {
                throw new ArgumentOutOfRangeException(nameof(score));
            }

            if (sampleCount < 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));

            Score = score;
            Status = status;
            SampleCount = sampleCount;
            ComparisonSemanticKey = comparisonSemanticKey ?? string.Empty;
            LastUpdatedRevision = lastUpdatedRevision;
        }

        public double? Score { get; }
        public PlayerDimensionStatus Status { get; }
        public int SampleCount { get; }
        public string ComparisonSemanticKey { get; }
        public long? LastUpdatedRevision { get; }
    }

    public sealed class PlayerProfileSnapshot
    {
        public PlayerProfileSnapshot(
            string userId,
            long profileRevision,
            string profileLineageId,
            PlayerDimensionSnapshot survival,
            PlayerDimensionSnapshot noise)
        {
            UserId = RequireText(userId, nameof(userId));
            if (profileRevision < 0) throw new ArgumentOutOfRangeException(nameof(profileRevision));
            ProfileRevision = profileRevision;
            ProfileLineageId = RequireText(profileLineageId, nameof(profileLineageId));
            Survival = survival ?? throw new ArgumentNullException(nameof(survival));
            Noise = noise ?? throw new ArgumentNullException(nameof(noise));
        }

        public string UserId { get; }
        public long ProfileRevision { get; }
        public string ProfileLineageId { get; }
        public PlayerDimensionSnapshot Survival { get; }
        public PlayerDimensionSnapshot Noise { get; }

        internal static string RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", name);
            return value;
        }
    }

    public sealed class RosterDimensionSummary
    {
        public RosterDimensionSummary(
            int observedActiveCount,
            int coldStartCount,
            int missingCount,
            int unsupportedCount,
            double observedCoverageRatio,
            RosterAggregationStatus aggregationStatus,
            string comparisonSemanticKey,
            double? meanObservedScore)
        {
            if (observedActiveCount < 0 || coldStartCount < 0 || missingCount < 0 || unsupportedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(observedActiveCount));
            }

            if (double.IsNaN(observedCoverageRatio) || double.IsInfinity(observedCoverageRatio)
                || observedCoverageRatio < 0d || observedCoverageRatio > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(observedCoverageRatio));
            }

            if (meanObservedScore.HasValue && (double.IsNaN(meanObservedScore.Value)
                || double.IsInfinity(meanObservedScore.Value) || meanObservedScore.Value < 0d || meanObservedScore.Value > 100d))
            {
                throw new ArgumentOutOfRangeException(nameof(meanObservedScore));
            }

            ObservedActiveCount = observedActiveCount;
            ColdStartCount = coldStartCount;
            MissingCount = missingCount;
            UnsupportedCount = unsupportedCount;
            ObservedCoverageRatio = observedCoverageRatio;
            AggregationStatus = aggregationStatus;
            ComparisonSemanticKey = comparisonSemanticKey ?? string.Empty;
            MeanObservedScore = meanObservedScore;
        }

        public int ObservedActiveCount { get; }
        public int ColdStartCount { get; }
        public int MissingCount { get; }
        public int UnsupportedCount { get; }
        public double ObservedCoverageRatio { get; }
        public RosterAggregationStatus AggregationStatus { get; }
        public string ComparisonSemanticKey { get; }
        public double? MeanObservedScore { get; }
    }

    public sealed class RosterProfileSummary
    {
        public RosterProfileSummary(
            string rosterIdentity,
            int teamSize,
            RosterDimensionSummary survival,
            RosterDimensionSummary noise)
        {
            RosterIdentity = PlayerProfileSnapshot.RequireText(rosterIdentity, nameof(rosterIdentity));
            if (teamSize < 1) throw new ArgumentOutOfRangeException(nameof(teamSize));
            TeamSize = teamSize;
            Survival = survival ?? throw new ArgumentNullException(nameof(survival));
            Noise = noise ?? throw new ArgumentNullException(nameof(noise));
        }

        public string RosterIdentity { get; }
        public int TeamSize { get; }
        public RosterDimensionSummary Survival { get; }
        public RosterDimensionSummary Noise { get; }
    }

    public sealed class ProfileRevisionRef
    {
        public ProfileRevisionRef(string userId, long profileRevision)
        {
            UserId = PlayerProfileSnapshot.RequireText(userId, nameof(userId));
            if (profileRevision < 0) throw new ArgumentOutOfRangeException(nameof(profileRevision));
            ProfileRevision = profileRevision;
        }

        public string UserId { get; }
        public long ProfileRevision { get; }
    }

    public sealed class AdaptiveInputProvenance
    {
        public AdaptiveInputProvenance(
            string profileFormulaSemanticId,
            string survivalComparisonKey,
            string noiseComparisonKey,
            string teamPerformanceFormulaVersion,
            IReadOnlyList<ProfileRevisionRef> sourceProfileRevisions,
            string currentMatchTelemetrySchemaVersion)
        {
            ProfileFormulaSemanticId = PlayerProfileSnapshot.RequireText(profileFormulaSemanticId, nameof(profileFormulaSemanticId));
            SurvivalComparisonKey = PlayerProfileSnapshot.RequireText(survivalComparisonKey, nameof(survivalComparisonKey));
            NoiseComparisonKey = PlayerProfileSnapshot.RequireText(noiseComparisonKey, nameof(noiseComparisonKey));
            TeamPerformanceFormulaVersion = teamPerformanceFormulaVersion ?? string.Empty;
            SourceProfileRevisions = Copy(sourceProfileRevisions);
            CurrentMatchTelemetrySchemaVersion = currentMatchTelemetrySchemaVersion ?? string.Empty;
        }

        public string ProfileFormulaSemanticId { get; }
        public string SurvivalComparisonKey { get; }
        public string NoiseComparisonKey { get; }
        public string TeamPerformanceFormulaVersion { get; }
        public IReadOnlyList<ProfileRevisionRef> SourceProfileRevisions { get; }
        public string CurrentMatchTelemetrySchemaVersion { get; }

        private static IReadOnlyList<ProfileRevisionRef> Copy(IReadOnlyList<ProfileRevisionRef> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = new List<ProfileRevisionRef>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                copy.Add(values[i] ?? throw new ArgumentNullException(nameof(values)));
            }
            return copy.AsReadOnly();
        }
    }

    public sealed class AdaptiveInputSnapshot
    {
        public AdaptiveInputSnapshot(
            Guid snapshotId,
            string snapshotContentFingerprint,
            Guid targetMatchId,
            ScenarioDecisionPoint decisionPoint,
            string phaseContext,
            DateTime createdAtUtc,
            string rosterIdentity,
            int teamSize,
            IReadOnlyList<PlayerProfileSnapshot> playerProfileSnapshots,
            RosterProfileSummary rosterProfileSummary,
            SnapshotValidity snapshotValidity,
            IReadOnlyList<string> reasonCodes,
            AdaptiveInputProvenance provenance)
        {
            if (snapshotId == Guid.Empty) throw new ArgumentException("Snapshot id is required.", nameof(snapshotId));
            if (targetMatchId == Guid.Empty) throw new ArgumentException("Target match id is required.", nameof(targetMatchId));
            if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Snapshot creation time must be UTC.", nameof(createdAtUtc));
            if (teamSize < 1) throw new ArgumentOutOfRangeException(nameof(teamSize));

            SnapshotId = snapshotId;
            SnapshotContentFingerprint = PlayerProfileSnapshot.RequireText(snapshotContentFingerprint, nameof(snapshotContentFingerprint));
            TargetMatchId = targetMatchId;
            DecisionPoint = decisionPoint;
            PhaseContext = phaseContext ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
            RosterIdentity = PlayerProfileSnapshot.RequireText(rosterIdentity, nameof(rosterIdentity));
            TeamSize = teamSize;
            PlayerProfileSnapshots = CopySnapshots(playerProfileSnapshots);
            RosterProfileSummary = rosterProfileSummary ?? throw new ArgumentNullException(nameof(rosterProfileSummary));
            SnapshotValidity = snapshotValidity;
            ReasonCodes = CopyStrings(reasonCodes);
            Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        }

        public Guid SnapshotId { get; }
        public string SnapshotContentFingerprint { get; }
        public Guid TargetMatchId { get; }
        public ScenarioDecisionPoint DecisionPoint { get; }
        public string PhaseContext { get; }
        public DateTime CreatedAtUtc { get; }
        public string RosterIdentity { get; }
        public int TeamSize { get; }
        public IReadOnlyList<PlayerProfileSnapshot> PlayerProfileSnapshots { get; }
        public RosterProfileSummary RosterProfileSummary { get; }
        public SnapshotValidity SnapshotValidity { get; }
        public IReadOnlyList<string> ReasonCodes { get; }
        public AdaptiveInputProvenance Provenance { get; }

        private static IReadOnlyList<PlayerProfileSnapshot> CopySnapshots(IReadOnlyList<PlayerProfileSnapshot> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = new List<PlayerProfileSnapshot>(values.Count);
            for (var i = 0; i < values.Count; i++) copy.Add(values[i] ?? throw new ArgumentNullException(nameof(values)));
            return copy.AsReadOnly();
        }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            if (values == null) return Array.Empty<string>();
            var copy = new List<string>(values.Count);
            for (var i = 0; i < values.Count; i++) copy.Add(values[i] ?? string.Empty);
            return copy.AsReadOnly();
        }
    }
}
