namespace EchoProtocol.Api.DTOs.Scenarios;

public sealed record AEDDirectorResearchRequestV1(Guid DecisionId, int CompletedPhaseOrdinal, string DirectorKind, string PolicyVersion, string RosterIdentity, string SnapshotFingerprint, string EvidenceFingerprint, string SourceFingerprint, string PayloadFingerprint, long ExpectedRevision, string PayloadJson);

public sealed record AEDDirectorResearchResponseV1(Guid MatchId, Guid DecisionId, int CompletedPhaseOrdinal, string DirectorKind, string PolicyVersion, string RosterIdentity, string SnapshotFingerprint, string EvidenceFingerprint, string SourceFingerprint, string PayloadFingerprint, long ExpectedRevision, string ApprovalScope, string Status, string ReasonCode, bool IsReplay, bool GameplayApproved);
