namespace EchoProtocol.Api.Entities;

public sealed class RemoteFixedBalanceVersion
{
    public string Version { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string ProtocolVersion { get; set; } = "RFB-1";
    public string SnapshotJson { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
