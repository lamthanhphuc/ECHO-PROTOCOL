using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class ProgressionPolicyV2 : IProgressionPolicy
{
    public const string PolicyVersion = "PROGRESSION_V2";
    private readonly RewardConfiguration _settings;
    public ProgressionPolicyV2(RewardConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.HasValidV2Settings()) throw new ArgumentException("Invalid V2 progression configuration.");
        _settings = settings;
    }
    public string Version => PolicyVersion;
    public bool IsConfigured => true;

    public IReadOnlyList<ProgressionAllocation> Calculate(RewardMatchSnapshot match)
    {
        RewardSnapshotRules.Validate(match);
        if (match.DurationSeconds <= 0) throw new ArgumentException("Invalid match duration.");
        return match.Players.Select(player =>
        {
            var (units, factor) = RewardEligibilityV2.Evaluate(player, _settings);
            if (units == 0) return new ProgressionAllocation(player.UserId, 0, false, false);
            var win = match.Outcome == MatchOutcome.WIN && !player.Disconnected ? _settings.WinExperience : 0m;
            var objective = decimal.Floor(match.ObjectiveCompletion * _settings.ObjectiveExperience);
            var contribution = decimal.Floor((decimal)_settings.ContributionExperience * units /
                (_settings.ObjectiveActionCap + _settings.RescueActionCap));
            var survival = player.Survived && !player.Disconnected ? _settings.SurvivalExperience : 0m;
            long xp = checked((long)(decimal.Floor((win + objective) * factor) + contribution + survival));
            if (match.Outcome == MatchOutcome.LOSE || player.Disconnected)
                xp = Math.Min(xp, checked((long)decimal.Floor(
                    _settings.LossExperiencePerMinute * match.DurationSeconds / 60m)));
            return new ProgressionAllocation(player.UserId, xp,
                match.Outcome == MatchOutcome.WIN && !player.Disconnected);
        }).ToArray();
    }

    public int GetLevel(long totalExperiencePoints) => new ProgressionPolicyV1().GetLevel(totalExperiencePoints);
}
