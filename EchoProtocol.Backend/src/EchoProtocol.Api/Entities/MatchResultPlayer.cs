namespace EchoProtocol.Api.Entities;

public sealed class MatchResultPlayer
{
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public bool Survived { get; set; }
    public bool Disconnected { get; set; }
    public int DetectionCount { get; set; }
    public int DownedCount { get; set; }
    public int ReviveCount { get; set; }
    public int ObjectiveContribution { get; set; }

    public MatchResult MatchResult { get; set; } = null!;
    public MatchPlayerBinding PlayerBinding { get; set; } = null!;
    public MatchRewardGrant? RewardGrant { get; set; }
    public ICollection<MatchScore> MatchScores { get; set; } = [];
}
