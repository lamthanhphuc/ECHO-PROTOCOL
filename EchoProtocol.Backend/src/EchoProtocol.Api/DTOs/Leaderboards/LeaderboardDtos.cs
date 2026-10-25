namespace EchoProtocol.Api.DTOs.Leaderboards;

public enum LeaderboardMode
{
    SOLO,
    TEAM
}

public sealed class LeaderboardPageResponse
{
    public IReadOnlyList<LeaderboardEntryResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public IReadOnlyList<string> Maps { get; init; } = [];
    public IReadOnlyList<string> Difficulties { get; init; } = [];
}

public sealed class LeaderboardEntryResponse
{
    public int Rank { get; init; }
    public Guid MatchId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public int DurationSeconds { get; init; }
    public int DurationCentiseconds { get; init; }
    public string? SquadLabel { get; init; }
    public IReadOnlyList<LeaderboardPlayerResponse> Players { get; init; } = [];
}

public sealed class LeaderboardPlayerResponse
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
