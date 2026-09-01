namespace EchoProtocol.Api.Entities;

public sealed class MatchPlayerBinding
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public int FusionActorNumber { get; set; }
    public Guid JoinProofId { get; set; }
    public DateTime BoundAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime? DisconnectedAtUtc { get; set; }

    public MatchAuthorityBinding? Match { get; set; }
    public User? User { get; set; }
}
