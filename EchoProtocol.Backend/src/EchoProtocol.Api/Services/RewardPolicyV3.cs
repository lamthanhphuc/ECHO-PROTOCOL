using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class RewardPolicyV3 : IRewardPolicy
{
    public const string PolicyVersion = "REWARD_V3";
    private readonly RewardConfiguration _settings;

    public RewardPolicyV3(RewardConfiguration settings)
    {
        if (!settings.HasValidV3Settings()) throw new ArgumentException("Invalid V3 reward configuration.");
        _settings = settings;
    }

    public string Version => PolicyVersion;
    public bool IsConfigured => true;

    internal decimal Multiplier(RewardMatchSnapshot match)
    {
        decimal difficulty = match.Difficulty switch
        {
            "EASY" => _settings.EasyMultiplier,
            "NORMAL" => 1m,
            "HARD" => _settings.HardMultiplier,
            _ => throw new ArgumentException("Missing authoritative difficulty.")
        };
        return Math.Clamp(ReferenceMinutes(match) / _settings.BaseMinutes, .75m, 1.5m) * difficulty;
    }

    internal decimal ReferenceMinutes(RewardMatchSnapshot match) => match.MapId switch
    {
        "SciFi" or "M2-MAP-1" => _settings.SciFiReferenceMinutes,
        "Scene_A" or "EXPEDITION-SCENE-A" => _settings.FloodedReferenceMinutes,
        "Scene_B" or "PURGE-SCENE-B" => _settings.PurgeReferenceMinutes,
        _ => throw new ArgumentException("Missing authoritative map.")
    };

    public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match)
    {
        RewardSnapshotRules.Validate(match);
        if (match.DurationSeconds <= 0) throw new ArgumentException("Invalid match duration.");
        decimal multiplier = Multiplier(match);
        return match.Players.Select(player =>
        {
            var (units, factor) = RewardEligibilityV2.Evaluate(player, _settings);
            if (units == 0) return new RewardAllocation(player.UserId, 0);
            bool won = match.Outcome == MatchOutcome.WIN && !player.Disconnected;
            decimal team = decimal.Floor(((won ? _settings.Win : 0)
                + decimal.Floor(match.ObjectiveCompletion * _settings.Objective)) * factor);
            decimal contribution = decimal.Floor(_settings.Contribution * units / 6m)
                + _settings.RescueBonusCredits * Math.Min(player.RescueActions!.Value, 2);
            decimal survival = player.Survived && !player.Disconnected ? _settings.Survival : 0;
            decimal potential = Math.Min(_settings.Maximum, team + contribution + survival) * multiplier;
            decimal awarded = won ? decimal.Round(potential, 0, MidpointRounding.AwayFromZero)
                : decimal.Floor(Math.Min(potential, _settings.V3LossCreditsPerMinute * CreditedMinutes(match, player)));
            return new RewardAllocation(player.UserId, checked((int)awarded));
        }).ToArray();
    }

    internal decimal CreditedMinutes(RewardMatchSnapshot match, RewardPlayerSnapshot player)
    {
        if (player.ParticipationSeconds is < 0)
            throw new ArgumentException("Invalid player participation duration.");
        int duration = Math.Min(match.DurationSeconds, player.ParticipationSeconds ?? match.DurationSeconds);
        return Math.Min(duration / 60m, Math.Min(ReferenceMinutes(match), 30m));
    }
}
