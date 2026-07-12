namespace EchoProtocol.Api.Entities;

public class PlayerProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public int TotalWins { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
