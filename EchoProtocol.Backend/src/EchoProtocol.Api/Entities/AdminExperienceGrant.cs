namespace EchoProtocol.Api.Entities;

public sealed class AdminExperienceGrant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AdminUserId { get; set; }
    public int Amount { get; set; }
    public long ExperienceBefore { get; set; }
    public long ExperienceAfter { get; set; }
    public int LevelBefore { get; set; }
    public int LevelAfter { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
