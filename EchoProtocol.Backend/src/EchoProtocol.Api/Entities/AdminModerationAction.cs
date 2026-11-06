namespace EchoProtocol.Api.Entities;

public sealed class AdminModerationAction
{
    public Guid Id { get; set; }
    public Guid TargetId { get; set; }
    public string Action { get; set; } = "";
    public Guid AdminUserId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
