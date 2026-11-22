using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IRewardPolicy
{
    string Version { get; }
    bool IsConfigured { get; }
    IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match);
}

public sealed record RewardMatchSnapshot(
    Guid MatchId,
    MatchOutcome Outcome,
    decimal ObjectiveCompletion,
    int DurationSeconds,
    IReadOnlyList<RewardPlayerSnapshot> Players,
    string? MapId = null,
    string? Difficulty = null);

public sealed record RewardPlayerSnapshot(
    Guid UserId,
    bool Survived,
    bool Disconnected,
    int DetectionCount,
    int DownedCount,
    int ReviveCount,
    int ObjectiveContribution,
    int? ObjectiveActions = null,
    int? RescueActions = null,
    bool? EvidenceComplete = null,
    int? ParticipationSeconds = null);

public sealed record RewardAllocation(Guid UserId, int CurrencyAmount);
