using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.Rewards;

public sealed class RewardMeResponse
{
    public Guid MatchId { get; init; }
    public MatchRewardStatus RewardStatus { get; init; }
    public int CurrencyAmount { get; init; }
    public int BalanceBefore { get; init; }
    public int BalanceAfter { get; init; }
    public long ExperiencePointsAwarded { get; init; }
    public long CurrentExperiencePoints { get; init; }
    public int CurrentLevel { get; init; }
    public string PolicyVersion { get; init; } = string.Empty;
    public string ProgressionPolicyVersion { get; init; } = string.Empty;
    public DateTime ProcessedAtUtc { get; init; }
}
