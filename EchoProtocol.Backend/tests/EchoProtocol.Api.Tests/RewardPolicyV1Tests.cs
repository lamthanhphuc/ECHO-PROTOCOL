using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class RewardPolicyV1Tests
{
    private readonly RewardPolicyV1 _policy = new();

    [Fact, Trait("Category", "M4RewardUnit")]
    public void CleanWinReceivesMaximumReward()
    {
        var allocation = SinglePlayer(
            MatchOutcome.WIN,
            objectiveCompletion: 1m,
            survived: true,
            objectiveContribution: 1);

        Assert.Equal(130, allocation.CurrencyAmount);
        Assert.Equal(RewardPolicyV1.MaximumCurrencyPerMatch, allocation.CurrencyAmount);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public void TeamWinWithoutSurvivalReceivesOneHundredTenCredits()
    {
        var allocation = SinglePlayer(
            MatchOutcome.WIN,
            objectiveCompletion: 1m,
            survived: false,
            objectiveContribution: 1);

        Assert.Equal(110, allocation.CurrencyAmount);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public void PartialProgressLossStillRewardsParticipation()
    {
        var allocation = SinglePlayer(
            MatchOutcome.LOSE,
            objectiveCompletion: 0.5m,
            survived: true,
            objectiveContribution: 1);

        Assert.Equal(65, allocation.CurrencyAmount);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public void DisconnectedPlayerReceivesFixedReducedReward()
    {
        var allocation = SinglePlayer(
            MatchOutcome.WIN,
            objectiveCompletion: 1m,
            survived: true,
            objectiveContribution: int.MaxValue,
            disconnected: true,
            reviveCount: 99);

        Assert.Equal(10, allocation.CurrencyAmount);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public void ReviveDetectionAndDownedCountsDoNotChangeReward()
    {
        var playerId = Guid.NewGuid();
        var baseline = Snapshot(playerId, reviveCount: 0, detectionCount: 0, downedCount: 0);
        var noisy = Snapshot(playerId, reviveCount: 9, detectionCount: 20, downedCount: 5);

        Assert.Equal(
            _policy.Calculate(baseline).Single().CurrencyAmount,
            _policy.Calculate(noisy).Single().CurrencyAmount);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public void ObjectiveContributionIsBinaryForV1()
    {
        var one = SinglePlayer(MatchOutcome.WIN, 1m, true, 1);
        var many = SinglePlayer(MatchOutcome.WIN, 1m, true, int.MaxValue);

        Assert.Equal(one.CurrencyAmount, many.CurrencyAmount);
    }

    [Fact, Trait("Category", "M4RewardUnit")]
    public void InvalidSnapshotIsRejected()
    {
        var invalid = new RewardMatchSnapshot(
            Guid.NewGuid(),
            MatchOutcome.WIN,
            1.1m,
            120,
            [new RewardPlayerSnapshot(Guid.NewGuid(), true, false, 0, 0, 0, 1)]);

        Assert.Throws<ArgumentException>(() => _policy.Calculate(invalid));
    }

    private RewardAllocation SinglePlayer(
        MatchOutcome outcome,
        decimal objectiveCompletion,
        bool survived,
        int objectiveContribution,
        bool disconnected = false,
        int reviveCount = 0)
    {
        var playerId = Guid.NewGuid();
        var snapshot = new RewardMatchSnapshot(
            Guid.NewGuid(),
            outcome,
            objectiveCompletion,
            2700,
            [new RewardPlayerSnapshot(
                playerId,
                survived,
                disconnected,
                0,
                0,
                reviveCount,
                objectiveContribution)]);

        return _policy.Calculate(snapshot).Single();
    }

    private static RewardMatchSnapshot Snapshot(
        Guid playerId,
        int reviveCount,
        int detectionCount,
        int downedCount) => new(
        Guid.NewGuid(),
        MatchOutcome.WIN,
        1m,
        2700,
        [new RewardPlayerSnapshot(
            playerId,
            true,
            false,
            detectionCount,
            downedCount,
            reviveCount,
            1)]);
}
