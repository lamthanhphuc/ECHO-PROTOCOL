using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class ProgressionPolicyV2Tests
{
    private readonly ProgressionPolicyV2 policy = new(new EchoProtocol.Api.Entities.RewardConfiguration());
    private static RewardMatchSnapshot Match(int actions, int rescues = 0,
        MatchOutcome outcome = MatchOutcome.WIN, int seconds = 2700, bool disconnected = false) =>
        new(Guid.NewGuid(), outcome, 1m, seconds,
            [new RewardPlayerSnapshot(Guid.NewGuid(), true, disconnected, 0, 0, 0, 0, actions, rescues, true)]);

    [Fact] public void FullWinReceivesMaximumV2Experience() =>
        Assert.Equal(175, policy.Calculate(Match(4, 2)).Single().ExperiencePoints);
    [Fact] public void NoActionsReceivesZeroExperience() =>
        Assert.Equal(0, policy.Calculate(Match(0)).Single().ExperiencePoints);
    [Fact] public void NoActionsDoesNotCountAsMatch() =>
        Assert.False(policy.Calculate(Match(0)).Single().CountsAsMatch);
    [Fact] public void ShortLossCannotFarmExperience() =>
        Assert.Equal(1, policy.Calculate(Match(4, 2, MatchOutcome.LOSE, 60)).Single().ExperiencePoints);
    [Fact] public void DisconnectRetainsLimitedEarnedExperience() =>
        Assert.Equal(1, policy.Calculate(Match(4, 2, seconds: 60, disconnected: true)).Single().ExperiencePoints);
    [Fact] public void LevelCalculationMatchesV1() =>
        Assert.Equal(new ProgressionPolicyV1().GetLevel(1000), policy.GetLevel(1000));
    [Fact] public void ConfiguredExperienceAndLossCapApply() {
        var policy = new ProgressionPolicyV2(new EchoProtocol.Api.Entities.RewardConfiguration {
            WinExperience = 100, ObjectiveExperience = 50,
            ContributionExperience = 0, SurvivalExperience = 0,
            LossExperiencePerMinute = 3
        });
        Assert.Equal(75, policy.Calculate(Match(1)).Single().ExperiencePoints);
        Assert.Equal(3, policy.Calculate(Match(4, 2, MatchOutcome.LOSE, 60)).Single().ExperiencePoints);
    }
}
