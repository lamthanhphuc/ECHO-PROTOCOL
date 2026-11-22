using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class RewardPolicyV1 : IRewardPolicy
{
    public const string PolicyVersion = "REWARD_V1";
    public const int MaximumCurrencyPerMatch = 130;

    private readonly RewardConfiguration _settings;
    public RewardPolicyV1() : this(new RewardConfiguration
    {
        PolicyKey = PolicyVersion, Participation = 20, Objective = 30, Win = 50,
        Survival = 20, Contribution = 10, Disconnected = 10, Maximum = 130
    }) { }
    public RewardPolicyV1(RewardConfiguration settings) => _settings = settings;
    public string Version => _settings.Id == Guid.Empty ? PolicyVersion : "REWARD_CUSTOM_V1";
    public bool IsConfigured => true;

    public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match)
    {
        RewardSnapshotRules.Validate(match);

        var objectiveCurrency = (int)decimal.Floor(
            match.ObjectiveCompletion * _settings.Objective);

        return match.Players
            .Select(player => new RewardAllocation(
                player.UserId,
                player.Disconnected
                    ? Math.Min(_settings.Disconnected, _settings.Maximum)
                    : CalculateConnectedPlayer(match.Outcome, objectiveCurrency, player)))
            .ToArray();
    }

    private int CalculateConnectedPlayer(
        MatchOutcome outcome,
        int objectiveCurrency,
        RewardPlayerSnapshot player)
    {
        var currency = checked(
            _settings.Participation
            + objectiveCurrency
            + (outcome == MatchOutcome.WIN ? _settings.Win : 0)
            + (player.Survived ? _settings.Survival : 0)
            + (player.ObjectiveContribution > 0 ? _settings.Contribution : 0));

        return Math.Min(currency, _settings.Maximum);
    }
}

internal static class RewardSnapshotRules
{
    public static void Validate(RewardMatchSnapshot match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.MatchId == Guid.Empty)
        {
            throw new ArgumentException("Match id is required.", nameof(match));
        }

        if (match.Outcome is not MatchOutcome.WIN and not MatchOutcome.LOSE)
        {
            throw new ArgumentException("Match outcome must be WIN or LOSE.", nameof(match));
        }

        if (match.ObjectiveCompletion is < 0 or > 1)
        {
            throw new ArgumentException(
                "Objective completion must be between 0 and 1.",
                nameof(match));
        }

        if (match.Players is null || match.Players.Count is < 1 or > 4)
        {
            throw new ArgumentException(
                "A reward snapshot must contain between 1 and 4 players.",
                nameof(match));
        }

        if (match.Players.Any(player =>
                player.UserId == Guid.Empty ||
                player.DetectionCount < 0 ||
                player.DownedCount < 0 ||
                player.ReviveCount < 0 ||
                player.ParticipationSeconds is < 0 ||
                player.ObjectiveContribution < 0) ||
            match.Players.Select(player => player.UserId).Distinct().Count() != match.Players.Count)
        {
            throw new ArgumentException("Reward snapshot player data is invalid.", nameof(match));
        }
    }
}
