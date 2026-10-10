using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDMetricStatusV1
    {
        Available,
        NoOpportunity,
        CensoredOnly,
        Unsupported,
        Incomplete,
        Invalid
    }

    public enum AEDMetricMeasurementKindV1
    {
        Count,
        DurationSeconds,
        Rate,
        Share,
        Ratio,
        Score
    }

    public sealed class AEDMetricResultV1
    {
        public const string ContractVersion = "AED_METRIC_OPPORTUNITY_V1";

        public string MetricId { get; }
        public string MetricVersion => ContractVersion;
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public string Zone { get; }
        public string UserId { get; }
        public string TeamKey { get; }
        public string Difficulty { get; }
        public string ScenarioResolutionMode { get; }
        public string ConfigSource { get; }
        public string ScenarioConfigVersion { get; }
        public string PolicyVersion { get; }
        public string SourceTelemetrySchemaVersion { get; }
        public long? AppliedPlanRevision { get; }
        public string AppliedParameterFingerprint { get; }
        public string SourceSystem { get; }
        public string AuthorityActor { get; }
        public IReadOnlyList<string> SourceEventIds { get; }
        public IReadOnlyList<string> OccurrenceKeys { get; }
        public IReadOnlyList<string> EpisodeIds { get; }
        public int EligibleOpportunities { get; }
        public int ResolvedSuccesses { get; }
        public int ResolvedFailures { get; }
        public int CensoredOpportunities { get; }
        public double Numerator { get; }
        public double? Denominator { get; }
        public string MeasurementUnit { get; }
        public double? Value { get; }
        public AEDMetricMeasurementKindV1 MeasurementKind { get; }
        public string DenominatorUnit { get; }
        public string ComparisonContextKey { get; }
        public IReadOnlyDictionary<string, int> CensorReasonCounts { get; }
        public double? ObservedDurationSeconds { get; }
        public int ConfidenceEvidenceCount { get; }
        public string ConfidenceStatus { get; }
        public AEDMetricStatusV1 Status { get; }
        public IReadOnlyList<string> ReasonCodes { get; }
        public DateTime WindowStartedAtUtc { get; }
        public DateTime WindowEndedAtUtc { get; }

        public AEDMetricResultV1(
            string metricId, Guid matchId, uint phaseOrdinal, string phaseName,
            string zone, string userId, string difficulty,
            string scenarioResolutionMode, string configSource,
            string scenarioConfigVersion, string policyVersion,
            string sourceTelemetrySchemaVersion, long? appliedPlanRevision,
            string appliedParameterFingerprint, string sourceSystem,
            string authorityActor, IEnumerable<string> sourceEventIds,
            IEnumerable<string> occurrenceKeys, IEnumerable<string> episodeIds,
            int eligibleOpportunities, int resolvedSuccesses,
            int resolvedFailures, int censoredOpportunities, double numerator,
            double? denominator, string measurementUnit, double? value,
            AEDMetricMeasurementKindV1 measurementKind, string denominatorUnit,
            string comparisonContextKey,
            IReadOnlyDictionary<string, int> censorReasonCounts,
            double? observedDurationSeconds, int confidenceEvidenceCount,
            string confidenceStatus, AEDMetricStatusV1 status,
            IEnumerable<string> reasonCodes, DateTime windowStartedAtUtc,
            DateTime windowEndedAtUtc)
        {
            MetricId = metricId ?? throw new ArgumentNullException(nameof(metricId));
            MatchId = matchId;
            PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName;
            Zone = zone;
            UserId = userId;
            TeamKey = matchId.ToString("D");
            Difficulty = difficulty;
            ScenarioResolutionMode = scenarioResolutionMode;
            ConfigSource = configSource;
            ScenarioConfigVersion = scenarioConfigVersion;
            PolicyVersion = policyVersion;
            SourceTelemetrySchemaVersion = sourceTelemetrySchemaVersion;
            AppliedPlanRevision = appliedPlanRevision;
            AppliedParameterFingerprint = appliedParameterFingerprint;
            SourceSystem = sourceSystem;
            AuthorityActor = authorityActor;
            SourceEventIds = Freeze(sourceEventIds);
            OccurrenceKeys = Freeze(occurrenceKeys);
            EpisodeIds = Freeze(episodeIds);
            EligibleOpportunities = eligibleOpportunities;
            ResolvedSuccesses = resolvedSuccesses;
            ResolvedFailures = resolvedFailures;
            CensoredOpportunities = censoredOpportunities;
            Numerator = numerator;
            Denominator = denominator;
            MeasurementUnit = measurementUnit;
            Value = value;
            MeasurementKind = measurementKind;
            DenominatorUnit = denominatorUnit;
            ComparisonContextKey = comparisonContextKey;
            CensorReasonCounts = new ReadOnlyDictionary<string, int>(
                censorReasonCounts == null
                    ? new Dictionary<string, int>()
                    : new Dictionary<string, int>(censorReasonCounts));
            ObservedDurationSeconds = observedDurationSeconds;
            ConfidenceEvidenceCount = confidenceEvidenceCount;
            ConfidenceStatus = confidenceStatus;
            Status = status;
            ReasonCodes = Freeze(reasonCodes);
            WindowStartedAtUtc = windowStartedAtUtc;
            WindowEndedAtUtc = windowEndedAtUtc;
        }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values) =>
            new ReadOnlyCollection<string>(values == null
                ? new List<string>()
                : new List<string>(values));
    }
}
