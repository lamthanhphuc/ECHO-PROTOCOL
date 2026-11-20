using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class RewardPolicyV2 : IRewardPolicy
{
    public const string PolicyVersion = "REWARD_V2";
    private readonly RewardConfiguration _settings;

    public RewardPolicyV2(RewardConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.HasValidV2Settings())
            throw new ArgumentException("Invalid V2 reward configuration.");
        _settings = settings;
    }

    public string Version => PolicyVersion;
    public bool IsConfigured => true;

    public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match)
    {
        RewardSnapshotRules.Validate(match);
        if (match.DurationSeconds <= 0) throw new ArgumentException("Invalid match duration.");
        return match.Players.Select(player =>
        {
            var (units, factor) = RewardEligibilityV2.Evaluate(player, _settings);
            if (units == 0) return new RewardAllocation(player.UserId, 0);
            var win = match.Outcome == MatchOutcome.WIN && !player.Disconnected ? _settings.Win : 0;
            var objective = decimal.Floor(match.ObjectiveCompletion * _settings.Objective);
            var team = decimal.Floor((win + objective) * factor);
            var contribution = decimal.Floor(_settings.Contribution * units /
                (_settings.ObjectiveActionCap + _settings.RescueActionCap))
                + _settings.RescueBonusCredits * Math.Min(player.RescueActions!.Value, _settings.RescueActionCap);
            var survival = player.Survived && !player.Disconnected ? _settings.Survival : 0;
            var raw = Math.Min(_settings.Maximum, checked((int)(team + contribution + survival)));
            if (match.Outcome == MatchOutcome.LOSE || player.Disconnected)
            {
                var cap = checked((int)decimal.Floor(_settings.LossCapCreditsPerMinute * match.DurationSeconds / 60m));
                raw = Math.Min(raw, cap);
            }
            return new RewardAllocation(player.UserId, raw);
        }).ToArray();
    }
}
