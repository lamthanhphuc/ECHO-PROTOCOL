using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class ProgressionPolicyV1Tests
{
    private readonly ProgressionPolicyV1 _policy = new();

    [Fact, Trait("Category", "M4ProfileUnit")]
    public void CleanWinReceivesMaximumExperienceAndCountsAsWin()
    {
        var allocation = SinglePlayer(
            MatchOutcome.WIN,
            objectiveCompletion: 1m,
            survived: true,
            objectiveContribution: 1);

        Assert.Equal(225, allocation.ExperiencePoints);
        Assert.True(allocation.CountsAsWin);
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public void PartialLossReceivesExpectedExperienceWithoutWin()
    {
        var allocation = SinglePlayer(
            MatchOutcome.LOSE,
            objectiveCompletion: 0.5m,
            survived: true,
            objectiveContribution: 1);

        Assert.Equal(125, allocation.ExperiencePoints);
        Assert.False(allocation.CountsAsWin);
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public void DisconnectedPlayerReceivesFixedExperienceAndNeverCountsAsWin()
    {
        var allocation = SinglePlayer(
            MatchOutcome.WIN,
            objectiveCompletion: 1m,
            survived: true,
            objectiveContribution: int.MaxValue,
            disconnected: true,
            reviveCount: 50);

        Assert.Equal(20, allocation.ExperiencePoints);
        Assert.False(allocation.CountsAsWin);
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public void ReviveCountDoesNotChangeExperience()
    {
        var playerId = Guid.NewGuid();
        var baseline = Snapshot(playerId, reviveCount: 0);
        var revivedOften = Snapshot(playerId, reviveCount: 20);

        Assert.Equal(
            _policy.Calculate(baseline).Single().ExperiencePoints,
            _policy.Calculate(revivedOften).Single().ExperiencePoints);
    }

    [Theory, Trait("Category", "M4ProfileUnit")]
    [InlineData(0L, 1)]
    [InlineData(499L, 1)]
    [InlineData(500L, 2)]
    [InlineData(999L, 2)]
    [InlineData(1000L, 3)]
    public void LevelUsesFiveHundredExperienceSteps(long experience, int expectedLevel)
    {
        Assert.Equal(expectedLevel, _policy.GetLevel(experience));
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public void VeryLargeExperienceDoesNotOverflowLevel()
    {
        Assert.Equal(int.MaxValue, _policy.GetLevel(long.MaxValue));
    }

    [Fact, Trait("Category", "M4ProfileUnit")]
    public void NegativeExperienceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.GetLevel(-1));
    }

    private ProgressionAllocation SinglePlayer(
        MatchOutcome outcome,
        decimal objectiveCompletion,
        bool survived,
        int objectiveContribution,
        bool disconnected = false,
        int reviveCount = 0)
    {
        var snapshot = new RewardMatchSnapshot(
            Guid.NewGuid(),
            outcome,
            objectiveCompletion,
            2700,
            [new RewardPlayerSnapshot(
                Guid.NewGuid(),
                survived,
                disconnected,
                0,
                0,
                reviveCount,
                objectiveContribution)]);

        return _policy.Calculate(snapshot).Single();
    }

    private static RewardMatchSnapshot Snapshot(Guid playerId, int reviveCount) => new(
        Guid.NewGuid(),
        MatchOutcome.WIN,
        1m,
        2700,
        [new RewardPlayerSnapshot(playerId, true, false, 0, 0, reviveCount, 1)]);
}
