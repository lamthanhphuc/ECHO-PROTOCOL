using System.Text.Json.Serialization;
using EchoProtocol.AI.Common.AED;

namespace EchoProtocol.Api.DTOs.Scenarios;

public sealed record AEDV02SourceRequest(int PhaseOrdinal, long StartTick, long EndTick, string SceneManifestFingerprint, bool AllowEmergencyWindow = false);
public sealed record AEDV02SourceResult(string Fingerprint, int PhaseOrdinal, long StartTick, long EndTick,
    string RosterIdentity, string SceneManifestFingerprint, Dictionary<Guid, double> PlayerPressure,
    Dictionary<Guid, double[]> PressureComponents, Dictionary<Guid, string> PressureEpisodeIds, Guid[] RecentDownUsers,
    bool SafeWindow, int LogicalPhase, int Zone, string PhaseName, int PlanRevision, string PlanFingerprint, DateTime SourceEndedAtUtc,
    bool PressureComplete, string[] MissingChannels, Guid[]? CabinetClueEventIds = null, string TeamPressureStatus = "Unsupported", AEDV02SupplySource? Supply = null,
    int Zone3MinionPopulation = -1, string OpportunityContractVersion = "", int TeamPressureTier = -1, double TeamPressureSeverity = 0,
    AEDV02ResolvedOpportunity[]? ResolvedOpportunities = null, string ComparisonContext = "", AEDV02ResourceNeed[]? ResourceNeeds = null, bool SupportSafeWindow = false);
public sealed record AEDV02DecisionRequest(Guid DecisionId, int PhaseOrdinal, string DecisionPoint, int WindowSequence,
    string WindowId, int ExpectedPlanRevision, string PreviousPlanFingerprint, double[] PlanValues,
    Guid SnapshotId, string SnapshotFingerprint, string RosterIdentity, string SourceFingerprint,
    long StartTick, long EndTick, string SceneManifestFingerprint, string PolicyVersion, string CatalogVersion,
    string ResolutionMode, string Difficulty, string TargetPhaseName, AEDV02ProposedReservation[]? ProposedReservations = null, AEDV02SupportAction? Support = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool SupportsRelativePreMatchRelief = false);
public sealed record AEDV02DecisionResult(Guid DecisionId, Guid MatchId, int PhaseOrdinal, string DecisionPoint,
    int WindowSequence, string WindowId, int PlanRevision, int ExpectedPlanRevision, string PreviousPlanFingerprint,
    string PlanFingerprint, double[] PlanValues, string RosterIdentity, string SourceFingerprint,
    string SceneManifestFingerprint, string PolicyVersion, string CatalogVersion, string ApprovalStatus,
    string ApplyStatus, string ReasonCode, DateTime ExpiresAtUtc, string TargetPhaseName, Guid SnapshotId, string SnapshotFingerprint, bool IsReplay, string ActionFingerprint = "",
    double[]? StalkerValues = null, double? PreviousChaseSpeed = null, double[]? ResolvedValues = null, int? OptionalToolMask = null, int InitialOptionalBudget = 0);
public sealed record AEDV02ReceiptRequest(Guid ReceiptId, Guid EffectEventId, int PlanRevision, string PlanFingerprint, long AppliedTick);
public sealed record AEDV02PlayerPressureRead(Guid UserId, double Score, double[] Components, int Tier, string EpisodeId);
public sealed record AEDV02SourceRead(string Fingerprint, int PhaseOrdinal, long StartTick, long EndTick,
    string RosterIdentity, string SceneManifestFingerprint, AEDV02PlayerPressureRead[] Players, Guid[] RecentDownUsers,
    bool SafeWindow, int LogicalPhase, int Zone, string PhaseName, int PlanRevision, string PlanFingerprint, DateTime SourceEndedAtUtc,
    bool PressureComplete, string[] MissingChannels, Guid[]? CabinetClueEventIds = null, string TeamPressureStatus = "Unsupported",
    string OpportunityContractVersion = "", int TeamPressureTier = -1, double TeamPressureSeverity = 0, string[]? IncreaseAxes = null,
    AEDV02ResourceNeed[]? ResourceNeeds = null, bool SupportSafeWindow = false, int OrdinarySupportRemaining = 0, int EmergencySupportRemaining = 0,
    DateTime? NextOrdinarySupportAtUtc = null);
public sealed record AEDV02HistoricalProfile(string ContractVersion, string ComparisonContext, string[] IncreaseAxes);
