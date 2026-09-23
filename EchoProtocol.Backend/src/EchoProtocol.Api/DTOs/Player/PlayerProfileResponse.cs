namespace EchoProtocol.Api.DTOs.Player;

public sealed class PlayerProfileResponse
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public int TotalMatches { get; init; }
    public int TotalWins { get; init; }
    public long ExperiencePoints { get; init; }
    public int Level { get; init; }
    public int WalletBalance { get; init; }
}
