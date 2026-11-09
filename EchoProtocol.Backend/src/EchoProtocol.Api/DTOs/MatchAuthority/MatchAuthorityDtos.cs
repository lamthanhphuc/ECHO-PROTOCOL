using System.ComponentModel.DataAnnotations;
using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.MatchAuthority;

public sealed class CreateMatchAuthorityRequest
{
    [Required, StringLength(128, MinimumLength = 1)]
    public string FusionSessionName { get; set; } = string.Empty;

    [Range(2, 4)]
    public int MaxPlayers { get; set; }
}

public sealed class MatchAuthorityResponse
{
    public IReadOnlyList<MatchRosterPlayerResponse> Players { get; set; } = Array.Empty<MatchRosterPlayerResponse>();
    public Guid MatchId { get; set; }
    public string FusionSessionName { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public int MaxPlayers { get; set; }
    public MatchAuthorityStatus Status { get; set; }
    public DateTime LeaseExpiresAtUtc { get; set; }
    public string? MapId { get; set; }
    public string? Difficulty { get; set; }
    public string? ResolutionMode { get; set; }
    public string? BalanceSource { get; set; }
    public string? BalanceProtocolVersion { get; set; }
    public string? BalanceVersion { get; set; }
    public string? BalanceHash { get; set; }
    public string? BalanceBuildVersion { get; set; }
    public string MatchDataPolicy { get; set; } = "STANDARD";
}

public sealed class MatchRosterPlayerResponse
{
    public Guid UserId { get; set; }
    public bool Disconnected { get; set; }
}

public sealed class StartMatchAuthorityRequest
{
    [StringLength(64)]
    public string? MapId { get; set; }

    [StringLength(16)]
    public string? Difficulty { get; set; }
}

public sealed class StartMatchV2Request
{
    public bool PrepareBalance { get; set; }
    public bool SupportsRelativeAdaptiveV02 { get; set; }
    [StringLength(80)] public string? GameVersion { get; set; }
    public bool SupportsGameplayTuning { get; set; }
    [Required, StringLength(64, MinimumLength = 1)]
    public string MapId { get; set; } = string.Empty;

    [Required, StringLength(16, MinimumLength = 1)]
    public string Difficulty { get; set; } = string.Empty;

    [Required, StringLength(16, MinimumLength = 1)]
    public string ResolutionMode { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 1)]
    public string BalanceSource { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 1)]
    public string BalanceProtocolVersion { get; set; } = string.Empty;
}

public sealed class LockedBalanceResponse
{
    public Guid MatchId { get; set; }
    public string BalanceVersion { get; set; } = string.Empty;
    public string BalanceHash { get; set; } = string.Empty;
    public string BalanceProtocolVersion { get; set; } = string.Empty;
    public string SnapshotPayloadBase64 { get; set; } = string.Empty;
}

public sealed class IssueJoinProofRequest
{
    [Range(1, int.MaxValue)]
    public int FusionActorNumber { get; set; }

    [Required, StringLength(128, MinimumLength = 1)]
    public string FusionSessionName { get; set; } = string.Empty;
}

public sealed class JoinProofResponse
{
    public string Proof { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class BindMatchPlayerRequest
{
    [Range(1, int.MaxValue)]
    public int FusionActorNumber { get; set; }

    [Required]
    public string JoinProof { get; set; } = string.Empty;
}

public sealed class MatchPlayerBindingResponse
{
    public Guid UserId { get; set; }
    public int FusionActorNumber { get; set; }
    public DateTime BoundAtUtc { get; set; }
}

public sealed class DisconnectMatchPlayerRequest
{
    [Required]
    public Guid ExpectedUserId { get; set; }
}

public sealed class TakeoverMatchAuthorityRequest
{
    [Required, StringLength(128, MinimumLength = 1)]
    public string FusionSessionName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int FusionActorNumber { get; set; }
}

public sealed class MatchPlayerEntitlementsResponse
{
    public Guid UserId { get; set; }
    public Guid[] ShopItemIds { get; set; } = [];
}

public sealed class EndMatchAuthorityRequest
{
    [StringLength(64)]
    public string Reason { get; set; } = "MATCH_ENDED";
}
