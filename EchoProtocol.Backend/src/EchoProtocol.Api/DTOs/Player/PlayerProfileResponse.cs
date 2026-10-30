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
    public PlayerActivityResponse? RecentMatch { get; init; }
    public PlayerActivityResponse? LatestWin { get; init; }
    public PlayerActivityResponse? LatestNonSurvival { get; init; }
}

public sealed class PlayerActivityResponse
{
    public Guid MatchId { get; init; }
    public DateTime EndedAtUtc { get; init; }
    public string? MapId { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public int DurationSeconds { get; init; }
}
