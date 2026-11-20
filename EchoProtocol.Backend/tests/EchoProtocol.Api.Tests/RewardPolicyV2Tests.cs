using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class RewardPolicyV2Tests
{
    [Fact] public void NewConfigurationDefaultsToV2Pilot() {
        var settings = new RewardConfiguration();
        Assert.Equal("REWARD_V2", settings.PolicyKey);
        Assert.Equal(320, settings.Maximum);
        Assert.Equal(0, settings.Participation);
        Assert.Equal(0, settings.Disconnected);
    }
    [Fact] public void ExistingV2SnapshotKeepsEarlierRuleDefaults() {
        var settings = System.Text.Json.JsonSerializer.Deserialize<RewardConfiguration>(
            "{\"PolicyKey\":\"REWARD_V2\"}")!;
        Assert.True(settings.HasValidV2Settings());
        Assert.Equal(4, settings.ObjectiveActionCap);
        Assert.Equal(2, settings.RescueActionCap);
        Assert.Equal(75, settings.WinExperience);
    }

    private static readonly RewardPolicyV2 Policy = new(new RewardConfiguration
    {
        PolicyKey = "REWARD_V2", Participation = 0, Objective = 70, Win = 150,
        Survival = 10, Contribution = 70, Disconnected = 0, Maximum = 320,
        LossCapCreditsPerMinute = 2m
    });

    private static RewardMatchSnapshot Match(int objectives, int rescues = 0,
        MatchOutcome outcome = MatchOutcome.WIN, int seconds = 2700, bool disconnected = false,
        bool complete = true) => new(Guid.NewGuid(), outcome, 1m, seconds,
        [new RewardPlayerSnapshot(Guid.NewGuid(), true, disconnected, 0, 0, 0, 0,
            objectives, rescues, complete)]);

    [Fact] public void FullContributionWinReceivesMaximum() =>
        Assert.Equal(320, Policy.Calculate(Match(4, 2)).Single().CurrencyAmount);
    [Fact] public void NoValidActionsReceivesZero() =>
        Assert.Equal(0, Policy.Calculate(Match(0)).Single().CurrencyAmount);
    [Fact] public void OneActionDoesNotUnlockFullTeamReward() =>
        Assert.Equal(131, Policy.Calculate(Match(1)).Single().CurrencyAmount);
    [Fact] public void RescuingTeammateAddsCreditsBeyondEligibilityFactor() =>
        Assert.Equal(141, Policy.Calculate(Match(0, 1)).Single().CurrencyAmount);
    [Fact] public void ConfiguredRescueBonusChangesReward() =>
        Assert.Equal(156, new RewardPolicyV2(new RewardConfiguration { RescueBonusCredits = 25 })
            .Calculate(Match(0, 1)).Single().CurrencyAmount);
    [Fact] public void EarlyLossWithoutContributionReceivesZero() =>
        Assert.Equal(0, Policy.Calculate(Match(0, outcome: MatchOutcome.LOSE, seconds: 30)).Single().CurrencyAmount);
    [Fact] public void LossRewardIsCappedByDuration() =>
        Assert.Equal(2, Policy.Calculate(Match(4, 2, MatchOutcome.LOSE, 60)).Single().CurrencyAmount);
    [Fact] public void DisconnectDoesNotEraseEarnedContribution() =>
        Assert.Equal(2, Policy.Calculate(Match(4, 2, seconds: 60, disconnected: true)).Single().CurrencyAmount);
    [Fact] public void InvalidEvidenceIsRejected() =>
        Assert.Throws<ArgumentException>(() => Policy.Calculate(Match(1, complete: false)));
    [Fact] public void RepeatedActionCountIsCapped() =>
        Assert.Equal(Policy.Calculate(Match(4, 2)).Single().CurrencyAmount,
            Policy.Calculate(Match(100, 100)).Single().CurrencyAmount);
    [Fact] public void ConfiguredActionCapsAndTeamFactorsApply() {
        var policy = new RewardPolicyV2(new RewardConfiguration {
            ObjectiveActionCap = 2, RescueActionCap = 1,
            OneActionTeamPercent = 20, TwoActionTeamPercent = 40, FullTeamPercent = 80,
            Win = 100, Objective = 0, Contribution = 0, Survival = 0, RescueBonusCredits = 0
        });
        Assert.Equal(20, policy.Calculate(Match(1)).Single().CurrencyAmount);
        Assert.Equal(40, policy.Calculate(Match(2)).Single().CurrencyAmount);
        Assert.Equal(80, policy.Calculate(Match(100, 100)).Single().CurrencyAmount);
    }
    [Fact] public void ThreeObjectivesAndTwoRescuesReachNewMaximum() {
        var policy = new RewardPolicyV2(new RewardConfiguration { ObjectiveActionCap = 3 });
        Assert.Equal(272, policy.Calculate(Match(3)).Single().CurrencyAmount);
        Assert.Equal(296, policy.Calculate(Match(3, 1)).Single().CurrencyAmount);
        Assert.Equal(320, policy.Calculate(Match(3, 2)).Single().CurrencyAmount);
    }
}
