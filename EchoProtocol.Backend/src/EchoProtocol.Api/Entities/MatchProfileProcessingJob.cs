namespace EchoProtocol.Api.Entities;

public sealed class MatchProfileProcessingJob
{
    public Guid MatchId { get; set; }
    public string Status { get; set; } = "PENDING";
    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string LastError { get; set; } = string.Empty;
}
