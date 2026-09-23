using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.Rewards;

public sealed class RewardProcessingResponse
{
    public Guid MatchId { get; init; }
    public MatchRewardStatus RewardStatus { get; init; }
    public string PolicyVersion { get; init; } = string.Empty;
    public bool IsReplay { get; init; }
    public DateTime ProcessedAtUtc { get; init; }
    public IReadOnlyList<RewardGrantResponse> Grants { get; init; } = [];
}

public sealed class RewardGrantResponse
{
    public Guid UserId { get; init; }
    public int CurrencyAmount { get; init; }
    public int BalanceBefore { get; init; }
    public int BalanceAfter { get; init; }
}
