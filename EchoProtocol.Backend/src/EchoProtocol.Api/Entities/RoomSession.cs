namespace EchoProtocol.Api.Entities;

public sealed class RoomSession
{
    public Guid Id { get; set; }
    public Guid CreateRequestId { get; set; }
    public string FusionSessionName { get; set; } = "";
    public string Region { get; set; } = "";
    public string? ActualRegion { get; set; }
    public Guid HostUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LeaseExpiresAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? CloseReason { get; set; }
    public Guid? CurrentMatchId { get; set; }
    public Guid? CurrentMatchRequestId { get; set; }
    public bool MatchPublished { get; set; }
}
