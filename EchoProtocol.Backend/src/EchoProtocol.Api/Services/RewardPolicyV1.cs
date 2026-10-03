using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class RewardPolicyV1 : IRewardPolicy
{
    public const string PolicyVersion = "REWARD_V1";
    public const int MaximumCurrencyPerMatch = 130;

    private const int ParticipationCurrency = 20;
    private const int MaximumObjectiveCurrency = 30;
    private const int WinCurrency = 50;
    private const int SurvivalCurrency = 20;
    private const int ContributionCurrency = 10;
    private const int DisconnectedCurrency = 10;

    public string Version => PolicyVersion;
    public bool IsConfigured => true;

    public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match)
    {
        RewardSnapshotRules.Validate(match);

        var objectiveCurrency = (int)decimal.Floor(
            match.ObjectiveCompletion * MaximumObjectiveCurrency);

        return match.Players
            .Select(player => new RewardAllocation(
                player.UserId,
                player.Disconnected
                    ? DisconnectedCurrency
                    : CalculateConnectedPlayer(match.Outcome, objectiveCurrency, player)))
            .ToArray();
    }

    private static int CalculateConnectedPlayer(
        MatchOutcome outcome,
        int objectiveCurrency,
        RewardPlayerSnapshot player)
    {
        var currency = checked(
            ParticipationCurrency
            + objectiveCurrency
            + (outcome == MatchOutcome.WIN ? WinCurrency : 0)
            + (player.Survived ? SurvivalCurrency : 0)
            + (player.ObjectiveContribution > 0 ? ContributionCurrency : 0));

        return Math.Min(currency, MaximumCurrencyPerMatch);
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
                player.ObjectiveContribution < 0) ||
            match.Players.Select(player => player.UserId).Distinct().Count() != match.Players.Count)
        {
            throw new ArgumentException("Reward snapshot player data is invalid.", nameof(match));
        }
    }
}
