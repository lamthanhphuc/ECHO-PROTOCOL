namespace EchoProtocol.Api.DTOs.Scenarios;

public sealed record AEDPhase8SourceReferenceV1(Guid EventId, long EventSequence, string EventType, string OccurrenceKey);
public sealed record AEDPhase8MetricEvidenceV1(Guid UserId, string MetricId, string MetricVersion, string ComparisonContextKey, int Eligible, int ResolvedSuccess, int ResolvedFailure, int Censored, decimal? NormalizedValue, string ConfidenceStatus, string[] SourceOccurrenceKeys);
public sealed record AEDPhase8SupplyEvidenceV1(string InstanceId, int ToolId, int Zone, string Origin, string Location, Guid? OwnerUserId, int? RemainingCharges, bool Reachable, long SourceTick, string SourceOccurrenceKey);
public sealed record AEDPhase8MinionEvidenceV1(string NetworkInstanceId, int Zone, string State, long SourceTick, string SourceOccurrenceKey);
public sealed record AEDPhase8SourceUploadRequestV1(int CompletedPhaseOrdinal, string EvidenceFingerprint, string RosterIdentity, string ComparisonContextKey, string SourceVersion, AEDPhase8SourceReferenceV1[] SourceEvents, AEDPhase8MetricEvidenceV1[] Metrics, AEDPhase8SupplyEvidenceV1[] Supply, AEDPhase8MinionEvidenceV1[] Minions);
public sealed record AEDPhase8SourceVerificationV1(bool CanonicalVerified, bool HistoricalSkillVerified, bool PressureVerified, bool SupplyLedgerVerified, bool MinionPopulationVerified, string ReasonCode);
