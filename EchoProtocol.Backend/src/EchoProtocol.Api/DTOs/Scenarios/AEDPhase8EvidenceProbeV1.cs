using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.DTOs.Scenarios;

public sealed class AEDPhase8EvidenceProbeRequestV1
{
    [Range(1, int.MaxValue)]
    public int CompletedEvidencePhaseOrdinal { get; set; }

    [Required, StringLength(64, MinimumLength = 64)]
    public string EvidenceFingerprint { get; set; } = string.Empty;
}

public sealed record AEDPhase8EvidenceProbeResultV1(
    Guid MatchId,
    int CompletedEvidencePhaseOrdinal,
    string EvidenceFingerprint,
    bool CanonicalEvidenceVerified,
    bool ExtendedMetricsVerified,
    bool ResourceLedgerVerified,
    bool GameplayApproved,
    string ReasonCode);
