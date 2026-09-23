using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class MatchResult
{
    public Guid MatchId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public MatchOutcome Outcome { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public int DurationSeconds { get; set; }
    public decimal ObjectiveCompletion { get; set; }
    public int PlayerCount { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public MatchRewardStatus RewardStatus { get; set; }
    public DateTime SubmittedAtUtc { get; set; }

    public MatchAuthorityBinding Match { get; set; } = null!;
    public User SubmittedByUser { get; set; } = null!;
    public ICollection<MatchResultPlayer> Players { get; set; } = [];
    public TeamProfile? TeamProfile { get; set; }
}
