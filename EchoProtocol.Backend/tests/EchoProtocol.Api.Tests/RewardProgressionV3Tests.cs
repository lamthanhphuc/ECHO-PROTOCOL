using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class RewardProgressionV3Tests
{
    private static RewardConfiguration Settings() => new() { PolicyKey = "REWARD_V3" };
    private static RewardMatchSnapshot Match(int actions = 4, int rescues = 2,
        MatchOutcome outcome = MatchOutcome.WIN, int seconds = 1200, bool disconnected = false,
        int? participation = null, decimal completion = 1m, bool survived = true) =>
        new(Guid.NewGuid(), outcome, completion, seconds,
            [new(Guid.NewGuid(), survived, disconnected, 0, 0, 0, 0, actions, rescues, true, participation)],
            "SciFi", "NORMAL");

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 0, 131, 91)]
    [InlineData(2, 0, 198, 126)]
    [InlineData(3, 0, 265, 162)]
    [InlineData(4, 0, 276, 166)]
    [InlineData(4, 2, 320, 175)]
    [InlineData(999, 999, 320, 175)]
    public void NeutralMapPreservesV2ContributionAndTeamFactor(int actions, int rescues, int credits, long xp)
    {
        var match = Match(actions, rescues);
        Assert.Equal(credits, new RewardPolicyV3(Settings()).Calculate(match).Single().CurrencyAmount);
        var progression = new ProgressionPolicyV3(Settings()).Calculate(match).Single();
        Assert.Equal(xp, progression.ExperiencePoints);
        Assert.Equal(actions + rescues > 0, progression.CountsAsMatch);
    }

    [Theory]
    [InlineData("SciFi", "EASY", 288, 158)]
    [InlineData("Scene_A", "NORMAL", 320, 175)]
    [InlineData("Scene_B", "HARD", 368, 201)]
    public void DefaultsDoNotInventLongerMapDurations(string map, string difficulty, int credits, long xp)
    {
        var match = Match() with { MapId = map, Difficulty = difficulty };
        Assert.Equal(credits, new RewardPolicyV3(Settings()).Calculate(match).Single().CurrencyAmount);
        Assert.Equal(xp, new ProgressionPolicyV3(Settings()).Calculate(match).Single().ExperiencePoints);
    }

    [Theory]
    [InlineData(1, 240, 131)]
    [InlineData(30, 480, 263)]
    [InlineData(120, 480, 263)]
    public void MapMultiplierIsClampedAndRoundsHalfAwayFromZero(int reference, int credits, long xp)
    {
        var settings = Settings(); settings.SciFiReferenceMinutes = reference;
        Assert.Equal(credits, new RewardPolicyV3(settings).Calculate(Match()).Single().CurrencyAmount);
        Assert.Equal(xp, new ProgressionPolicyV3(settings).Calculate(Match()).Single().ExperiencePoints);
    }

    [Fact]
    public void MaximumMapAndDifficultyMultiplierStaysBounded()
    {
        var settings = Settings(); settings.PurgeReferenceMinutes = 30;
        var match = Match() with { MapId = "Scene_B", Difficulty = "HARD" };
        Assert.Equal(552, new RewardPolicyV3(settings).Calculate(match).Single().CurrencyAmount);
        Assert.Equal(302, new ProgressionPolicyV3(settings).Calculate(match).Single().ExperiencePoints);
    }

    [Theory]
    [InlineData(30, 2, 1)]
    [InlineData(180, 12, 6)]
    [InlineData(300, 20, 10)]
    [InlineData(600, 40, 20)]
    [InlineData(1200, 80, 40)]
    [InlineData(2700, 80, 40)]
    public void LossUsesPotentialAndTimeCapWithoutAnExtraThirtyPercentPenalty(int seconds, int credits, long xp)
    {
        var match = Match(outcome: MatchOutcome.LOSE, seconds: seconds, completion: .75m, survived: false);
        Assert.Equal(credits, new RewardPolicyV3(Settings()).Calculate(match).Single().CurrencyAmount);
        Assert.Equal(xp, new ProgressionPolicyV3(Settings()).Calculate(match).Single().ExperiencePoints);
    }

    [Fact]
    public void DisconnectUsesPersonalParticipationAndReceivesNoWinOrSurvivalBonus()
    {
        var match = Match(disconnected: true, participation: 60);
        Assert.Equal(4, new RewardPolicyV3(Settings()).Calculate(match).Single().CurrencyAmount);
        var xp = new ProgressionPolicyV3(Settings()).Calculate(match).Single();
        Assert.Equal(2, xp.ExperiencePoints);
        Assert.False(xp.CountsAsWin);
    }

    [Fact]
    public void LossNeverExceedsEarnedPotentialOrThirtyMinutes()
    {
        var settings = Settings(); settings.SciFiReferenceMinutes = 120;
        var match = Match(1, 0, MatchOutcome.LOSE, 7200, completion: 0, survived: false);
        Assert.Equal(16, new RewardPolicyV3(settings).Calculate(match).Single().CurrencyAmount);
        Assert.Equal(6, new ProgressionPolicyV3(settings).Calculate(match).Single().ExperiencePoints);
        Assert.Equal(120, new RewardPolicyV3(settings).Calculate(Match(outcome: MatchOutcome.LOSE, seconds: 7200)).Single().CurrencyAmount);
    }

    [Fact]
    public void InvalidContextAndEvidenceFailClosed()
    {
        var policy = new RewardPolicyV3(Settings());
        Assert.Throws<ArgumentException>(() => policy.Calculate(Match() with { MapId = "UNKNOWN" }));
        Assert.Throws<ArgumentException>(() => policy.Calculate(Match() with { Difficulty = null }));
        Assert.Throws<ArgumentException>(() => policy.Calculate(Match(participation: -1)));
        var match = Match();
        Assert.Throws<ArgumentException>(() => policy.Calculate(match with { Players = [match.Players[0] with { EvidenceComplete = false }] }));
        var invalid = Settings(); invalid.FullTeamPercent = 99;
        Assert.Throws<ArgumentException>(() => new RewardPolicyV3(invalid));
    }

    [Fact]
    public void EveryLevelBoundaryIsMonotonicAndCapDoesNotOverflow()
    {
        for (int level = 2; level <= 30; level++)
        {
            long threshold = ProgressionCurveV3.Threshold(level);
            Assert.Equal(level - 1, ProgressionCurveV3.GetLevel(threshold - 1));
            Assert.Equal(level, ProgressionCurveV3.GetLevel(threshold));
            Assert.Equal(0, ProgressionCurveV3.RequiredForNextLevel(level) % 25);
        }
        Assert.Equal(3850, ProgressionCurveV3.Threshold(10));
        Assert.Equal(40850, ProgressionCurveV3.Threshold(30));
        Assert.Equal(30, ProgressionCurveV3.GetLevel(long.MaxValue));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(35)]
    public void DelayedV2GrantUsesMigratedProfileCurveAndNeverDowngradesLegacyLevel(int level)
    {
        var match = Match(1, 0);
        var profile = new PlayerProfile { Level = level, LevelCurveVersion = "LEVEL_V3",
            ExperiencePoints = ProgressionCurveV3.Threshold(Math.Min(level, 30)) };
        var result = new ProgressionService(new ProgressionPolicyV2(new RewardConfiguration()))
            .Calculate(match, new Dictionary<Guid, PlayerProfile> { [match.Players[0].UserId] = profile });
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(level, result.Data!.Single().Level);
        Assert.Equal(91, result.Data!.Single().ExperiencePointsAwarded);
    }
}
