using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class MatchAuthorityBinding
{
    public Guid MatchId { get; set; }
    public string FusionSessionName { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public int MaxPlayers { get; set; }
    public MatchAuthorityStatus Status { get; set; }
    public DateTime LeaseExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    public User? HostUser { get; set; }
    public ICollection<MatchPlayerBinding> Players { get; set; } = [];
}
