using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class ProgressionPolicyV3(RewardConfiguration settings) : IProgressionPolicy
{
    public const string PolicyVersion = "PROGRESSION_V3";
    public string Version => PolicyVersion;
    public bool IsConfigured => settings.HasValidV3Settings();
    public int GetLevel(long experience) => ProgressionCurveV3.GetLevel(experience);

    public IReadOnlyList<ProgressionAllocation> Calculate(RewardMatchSnapshot match)
    {
        RewardSnapshotRules.Validate(match);
        if (match.DurationSeconds <= 0) throw new ArgumentException("Invalid match duration.");
        var reward = new RewardPolicyV3(settings);
        decimal multiplier = reward.Multiplier(match);
        return match.Players.Select(player =>
        {
            var (units, factor) = RewardEligibilityV2.Evaluate(player, settings);
            if (units == 0) return new ProgressionAllocation(player.UserId, 0, false, false);
            bool won = match.Outcome == MatchOutcome.WIN && !player.Disconnected;
            decimal team = decimal.Floor(((won ? settings.WinExperience : 0)
                + decimal.Floor(match.ObjectiveCompletion * settings.ObjectiveExperience)) * factor);
            decimal contribution = decimal.Floor(settings.ContributionExperience * units / 6m);
            decimal survival = player.Survived && !player.Disconnected ? settings.SurvivalExperience : 0;
            decimal potential = (team + contribution + survival) * multiplier;
            decimal xp = won ? decimal.Round(potential, 0, MidpointRounding.AwayFromZero)
                : decimal.Floor(Math.Min(potential, settings.V3LossExperiencePerMinute * reward.CreditedMinutes(match, player)));
            return new ProgressionAllocation(player.UserId, checked((long)xp), won);
        }).ToArray();
    }
}
