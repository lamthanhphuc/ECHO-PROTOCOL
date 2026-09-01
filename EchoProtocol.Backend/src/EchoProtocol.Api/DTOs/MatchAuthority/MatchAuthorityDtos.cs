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
    public Guid MatchId { get; set; }
    public string FusionSessionName { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public int MaxPlayers { get; set; }
    public MatchAuthorityStatus Status { get; set; }
    public DateTime LeaseExpiresAtUtc { get; set; }
}

public sealed class IssueJoinProofRequest
{
    [Range(1, 4)]
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
    [Range(1, 4)]
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

public sealed class EndMatchAuthorityRequest
{
    [StringLength(64)]
    public string Reason { get; set; } = "MATCH_ENDED";
}
