namespace EchoProtocol.Api.Entities;

public sealed class RemoteFixedBalanceAuditEntry
{
    public Guid Id { get; set; }
    public string Action { get; set; } = "";
    public string MapId { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string ProtocolVersion { get; set; } = "RFB-1";
    public string? PreviousVersion { get; set; }
    public string? TargetVersion { get; set; }
    public Guid AdminUserId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
