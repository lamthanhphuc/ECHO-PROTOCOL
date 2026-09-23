using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.MatchResults;

public sealed class MatchResultResponse
{
    public Guid MatchId { get; set; }
    public MatchOutcome Outcome { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public int DurationSeconds { get; set; }
    public decimal ObjectiveCompletion { get; set; }
    public int PlayerCount { get; set; }
    public MatchRewardStatus RewardStatus { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public bool IsReplay { get; set; }
    public IReadOnlyList<MatchResultPlayerResponse> Players { get; set; } = [];
}

public sealed class MatchResultPlayerResponse
{
    public Guid UserId { get; set; }
    public bool Survived { get; set; }
    public bool Disconnected { get; set; }
    public int DetectionCount { get; set; }
    public int DownedCount { get; set; }
    public int ReviveCount { get; set; }
    public int ObjectiveContribution { get; set; }
}
