namespace EchoProtocol.Api.Entities;

public sealed class ProgressionCosmeticGrant
{
    public Guid UserId { get; set; }
    public string RewardKey { get; set; } = string.Empty;
    public DateTime GrantedAtUtc { get; set; }
    public bool Equipped { get; set; }
}
