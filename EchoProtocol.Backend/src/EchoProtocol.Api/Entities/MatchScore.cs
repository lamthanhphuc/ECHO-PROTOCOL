using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

/// <summary>
/// Immutable, dimension-scoped PlayerMatchScore observation and apply receipt.
/// ContributionStatus may change when canonical source evidence is invalidated;
/// the score and provenance never change.
/// </summary>
public sealed class MatchScore
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProfileLineageId { get; set; }
    public PlayerAIDimension Dimension { get; set; }
    public decimal Score { get; set; }
    public DateTime MatchEndTs { get; set; }
    public string MatchScoreFormulaVersion { get; set; } = string.Empty;
    public string NormalizationConfigVersion { get; set; } = string.Empty;
    public string? ProfileNoiseFilterVersion { get; set; }
    public string AlphaConfigVersion { get; set; } = string.Empty;
    public string SourceTelemetrySchemaVersion { get; set; } = string.Empty;
    public string SourceEvidenceFingerprint { get; set; } = string.Empty;
    public string SemanticFingerprint { get; set; } = string.Empty;
    public ProfileContributionStatus ContributionStatus { get; set; }
    public string? RetractionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RetractedAtUtc { get; set; }

    public MatchResultPlayer ResultPlayer { get; set; } = null!;
    public PlayerAIProfile PlayerAIProfile { get; set; } = null!;
}
