using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDMetricPolicyContextV1
    {
        public string Difficulty { get; }
        public string ScenarioResolutionMode { get; }
        public string ConfigSource { get; }
        public string ScenarioConfigVersion { get; }
        public string PolicyVersion { get; }
        public long? AppliedPlanRevision { get; }
        public string AppliedParameterFingerprint { get; }
        public string ComparisonContextKey { get; }

        public AEDMetricPolicyContextV1(string difficulty,
            string scenarioResolutionMode, string configSource,
            string scenarioConfigVersion, string policyVersion,
            long? appliedPlanRevision, string appliedParameterFingerprint,
            string comparisonContextKey)
        {
            Difficulty = difficulty;
            ScenarioResolutionMode = scenarioResolutionMode;
            ConfigSource = configSource;
            ScenarioConfigVersion = scenarioConfigVersion;
            PolicyVersion = policyVersion;
            AppliedPlanRevision = appliedPlanRevision;
            AppliedParameterFingerprint = appliedParameterFingerprint;
            ComparisonContextKey = comparisonContextKey;
        }
    }

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
        public bool DecisionEligible { get; }
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
            DateTime windowEndedAtUtc,
            AEDMetricPolicyContextV1 policyContext = null,
            bool sourceVerifiedForPolicy = false,
            int minimumResolvedOpportunitiesForPolicy = 0)
        {
            MetricId = metricId ?? throw new ArgumentNullException(nameof(metricId));
            MatchId = matchId;
            PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName;
            Zone = zone;
            UserId = userId;
            TeamKey = matchId.ToString("D");
            Difficulty = policyContext?.Difficulty ?? difficulty;
            ScenarioResolutionMode = policyContext?.ScenarioResolutionMode ?? scenarioResolutionMode;
            ConfigSource = policyContext?.ConfigSource ?? configSource;
            ScenarioConfigVersion = policyContext?.ScenarioConfigVersion ?? scenarioConfigVersion;
            PolicyVersion = policyContext?.PolicyVersion ?? policyVersion;
            SourceTelemetrySchemaVersion = sourceTelemetrySchemaVersion;
            AppliedPlanRevision = policyContext?.AppliedPlanRevision ?? appliedPlanRevision;
            AppliedParameterFingerprint = policyContext?.AppliedParameterFingerprint
                ?? appliedParameterFingerprint;
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
            ComparisonContextKey = policyContext?.ComparisonContextKey ?? comparisonContextKey;
            CensorReasonCounts = new ReadOnlyDictionary<string, int>(
                censorReasonCounts == null
                    ? new Dictionary<string, int>()
                    : new Dictionary<string, int>(censorReasonCounts));
            ObservedDurationSeconds = observedDurationSeconds;
            ConfidenceEvidenceCount = confidenceEvidenceCount;
            ConfidenceStatus = confidenceStatus;
            Status = status;
            WindowStartedAtUtc = windowStartedAtUtc;
            WindowEndedAtUtc = windowEndedAtUtc;
            var reasons = reasonCodes == null
                ? new List<string>() : new List<string>(reasonCodes);
            var policyContextComplete = !string.IsNullOrWhiteSpace(Difficulty)
                && !string.IsNullOrWhiteSpace(ScenarioResolutionMode)
                && !string.IsNullOrWhiteSpace(ConfigSource)
                && !string.IsNullOrWhiteSpace(ScenarioConfigVersion)
                && !string.IsNullOrWhiteSpace(PolicyVersion)
                && !string.IsNullOrWhiteSpace(ComparisonContextKey)
                && string.Equals(AuthorityActor, "FusionStateAuthority", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(SourceSystem)
                && !string.IsNullOrWhiteSpace(SourceTelemetrySchemaVersion)
                && WindowStartedAtUtc.Kind == DateTimeKind.Utc
                && WindowEndedAtUtc.Kind == DateTimeKind.Utc
                && WindowEndedAtUtc >= WindowStartedAtUtc
                && (ScenarioResolutionMode != "Adaptive"
                    || (AppliedPlanRevision.HasValue
                        && !string.IsNullOrWhiteSpace(AppliedParameterFingerprint)));
            var resolvedOpportunities =
                ResolvedSuccesses + ResolvedFailures;

            var confidenceSufficient = string.Equals(
                ConfidenceStatus,
                "SufficientForPolicy",
                StringComparison.Ordinal);

            var opportunitiesSufficient =
                minimumResolvedOpportunitiesForPolicy > 0
                && resolvedOpportunities >= minimumResolvedOpportunitiesForPolicy
                && ConfidenceEvidenceCount >= minimumResolvedOpportunitiesForPolicy
                && EligibleOpportunities >= resolvedOpportunities;

            DecisionEligible =
                status == AEDMetricStatusV1.Available
                && policyContextComplete
                && sourceVerifiedForPolicy
                && confidenceSufficient
                && opportunitiesSufficient
                && SourceEventIds.Count > 0
                && Denominator.HasValue
                && Denominator.Value > 0
                && Value.HasValue
                && !double.IsNaN(Value.Value)
                && !double.IsInfinity(Value.Value);

            if (status == AEDMetricStatusV1.Available && !DecisionEligible)
            {
                if (!policyContextComplete)
                    reasons.Add("POLICY_CONTEXT_INCOMPLETE");

                if (!sourceVerifiedForPolicy)
                    reasons.Add("POLICY_SOURCE_UNVERIFIED");

                if (!confidenceSufficient)
                    reasons.Add("CONFIDENCE_INSUFFICIENT");

                if (!opportunitiesSufficient)
                    reasons.Add("POLICY_MIN_OPPORTUNITIES_UNMET");
            }
            ReasonCodes = Freeze(reasons);
        }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values) =>
            new ReadOnlyCollection<string>(values == null
                ? new List<string>()
                : new List<string>(values));
    }
}
